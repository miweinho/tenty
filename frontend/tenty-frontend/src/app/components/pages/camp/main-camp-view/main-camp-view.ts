import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subject, forkJoin, of, takeUntil } from 'rxjs';

import { CampService } from '../../../../service/camps/camp';
import { FeatureService } from '../../../../service/feature/feature.service';
import { CampMainMapView } from '../../../maps/camp-main-map-view/camp-main-map-view';
import { MapToolbarComponent } from '../../../maps/map-toolbar/map-toolbar';
import { FeatureDto, FeatureKind, FullCampDto } from '../../../../models';
import { DrawingService } from '../../../../service/drawingService/drawing-service';
import { CampMapHelperService } from '../../../../service/campMapHelperService/camp-map-helper';
import { ShowCampFormComponent } from '../../../forms/show-camp-form/show-camp-form';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatToolbarModule } from '@angular/material/toolbar';

@Component({
  selector: 'app-main-camp-view',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CampMainMapView,
    MapToolbarComponent,
    ShowCampFormComponent,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatListModule,
    MatDividerModule,
    MatFormFieldModule,
    MatInputModule,
    MatToolbarModule,
  ],
  templateUrl: './main-camp-view.html',
  styleUrls: ['./main-camp-view.css'],
})
export class MainCampViewComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private campService = inject(CampService);
  private featureService = inject(FeatureService);
  private destroy$ = new Subject<void>();
  private drawingService = inject(DrawingService);
  private campMapHelper = inject(CampMapHelperService);

  camp: FullCampDto | null = null;
  features: FeatureDto[] = [];
  dirty = false;
  private mapReady = false;

  showCampInfo = false;
  selectedFeature: FeatureDto | null = null;
  featureDraft: FeatureDto | null = null;
  showFeaturePopup = false;

  editingCamp = false;
  campDraft: FullCampDto | null = null;
  showLocationPicker = false;
  showDelimPicker = false;

  private undoStack: { camp: FullCampDto | null; features: FeatureDto[] }[] = [];
  private redoStack: { camp: FullCampDto | null; features: FeatureDto[] }[] = [];
  private lastSavedSnapshot: { camp: FullCampDto | null; features: FeatureDto[] } | null = null;
  private deletedFeatureIds: number[] = [];

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe((params) => {
      const id = Number(params.get('id'));
      if (!isNaN(id)) {
        this.loadCamp(id);
        this.loadFeatures(id);
      }
    });

    this.featureService.features$.pipe(takeUntil(this.destroy$)).subscribe((list) => {
      this.features = list ?? [];
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadCamp(id: number): void {
    this.campService
      .loadCamp(id, true)
      .pipe(takeUntil(this.destroy$))
      .subscribe((camp: FullCampDto) => {
        this.camp = camp;
        this.setSavedSnapshot();
      });
  }

  private loadFeatures(id: number): void {
    this.featureService.loadFeaturesForCamp(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        // wenn Camp bereits geladen ist, Snapshot aktualisieren
        if (this.camp) {
          this.setSavedSnapshot();
        }
      },
      error: (err) => console.error('Load features failed', err),
    });
  }

  onFeatureSelected(feature: FeatureDto): void {
    this.selectedFeature = feature;
    this.featureDraft = structuredClone(feature);
    this.showFeaturePopup = true;
  }

  onStartDraw(kind: string): void {
    if (!this.mapReady) return;

    // map toolbar kinds -> FeatureKind
    const kindMap: Record<string, FeatureKind> = {
      tent: 'tent',
      water: 'water',
    };
    const featureKind = kindMap[kind] ?? 'tent';

    // geometry type: tent -> Point, water -> LineString
    const drawType = featureKind === 'water' ? 'LineString' : 'Point';

    // snapshot before change
    this.pushUndo();

    this.drawingService.drawOnce(drawType as any).then((feature) => {
      if (!feature) return;
      const geometryDto = this.campMapHelper.dtoFromFeature(feature);
      const newFeature: FeatureDto = {
        id: Math.floor(-Math.random() * 1_000_000), // temp negative id
        campId: this.camp?.id ?? 0,
        kind: featureKind,
        name: featureKind,
        geometry: geometryDto,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      this.features = [...this.features, newFeature];
      this.featureDraft = structuredClone(newFeature);
      this.showFeaturePopup = true;
      this.markDirty();
      this.clearRedo();
    });
  }

  onUndo(): void {
    if (!this.undoStack.length) return;
    const current = this.currentSnapshot();
    const prev = this.undoStack.pop()!;
    this.redoStack.push(current);
    this.applySnapshot(prev);
  }

  onRedo(): void {
    if (!this.redoStack.length) return;
    const current = this.currentSnapshot();
    const next = this.redoStack.pop()!;
    this.undoStack.push(current);
    this.applySnapshot(next);
  }

  onRefresh(): void {
    if (this.camp?.id) {
      this.loadCamp(this.camp.id);
      this.loadFeatures(this.camp.id);
      this.deletedFeatureIds = [];
      this.showFeaturePopup = false;
      this.selectedFeature = null;
    }
  }

  onSave(): void {
    if (!this.camp?.id) return;

    const newFeatures = this.features.filter((f) => f.id <= 0);
    const changedExisting = this.getChangedExistingFeatures();
    const deletedExisting = [...this.deletedFeatureIds];

    if (!newFeatures.length && !changedExisting.length && !deletedExisting.length) {
      this.dirty = false;
      this.setSavedSnapshot();
      this.clearRedo();
      this.undoStack = [];
      return;
    }

    const createRequests = newFeatures.map((f) =>
      this.featureService.createFeature({
        campId: f.campId,
        kind: f.kind,
        name: f.name ?? null,
        geometry: f.geometry,
        headingDeg: f.headingDeg ?? null,
        lengthM: f.lengthM ?? null,
        widthM: f.widthM ?? null,
        color: f.color ?? null,
        icon: f.icon ?? null,
        props: f.props ?? null,
        details: f.details ?? null,
      })
    );

    const updateRequests = changedExisting.map((f) => this.featureService.updateFeature(f.id, f));
    const deleteRequests = deletedExisting.map((id) => this.featureService.deleteFeature(id));

    const allRequests = [...createRequests, ...updateRequests, ...deleteRequests];

    forkJoin(allRequests.length ? allRequests : [of(null)]).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.loadFeatures(this.camp!.id);
        this.deletedFeatureIds = [];
        this.dirty = false;
        this.setSavedSnapshot();
        this.clearRedo();
        this.undoStack = [];
      },
      error: (err) => {
        console.error('Save features failed', err);
        this.dirty = true;
      },
    });
  }

  toggleCampInfo(): void {
    this.showCampInfo = !this.showCampInfo;
    if (this.showCampInfo && this.camp) {
      this.campDraft = structuredClone(this.camp);
      this.editingCamp = false;
    }
  }

  onMapReady(): void {
    this.mapReady = true;
  }

  startCampEdit(): void {
    if (this.camp) {
      this.editingCamp = true;
      this.campDraft = structuredClone(this.camp);
    }
  }

  saveCampEdit(): void {
    if (!this.camp || !this.campDraft) return;
    const dto = {
      name: this.campDraft.name,
      location: this.campDraft.location,
      delimitation: this.campDraft.delimitation,
      campDetail: this.campDraft.campDetail,
      isSubCamp: this.campDraft.isSubCamp,
      parentId: this.campDraft.parentId ?? 0,
    };

    this.campService.updateCamp(this.camp.id, dto).pipe(takeUntil(this.destroy$)).subscribe({
      next: (updated) => {
        this.camp = updated;
        this.campDraft = structuredClone(updated);
        this.editingCamp = false;
        this.markDirty();
        this.setSavedSnapshot();
      },
      error: (err) => console.error('Update camp failed', err),
    });
  }

  openLocationPicker(): void {
    this.showLocationPicker = true;
  }

  openDelimPicker(): void {
    this.showDelimPicker = true;
  }

  onLocationPicked(loc: any): void {
    if (!this.campDraft && this.camp) {
      this.campDraft = structuredClone(this.camp);
    }
    if (this.campDraft) {
      this.campDraft.location = loc;
      this.camp = { ...(this.camp ?? this.campDraft), location: loc } as FullCampDto;
      this.markDirty();
    }
    this.showLocationPicker = false;
  }

  onDelimPicked(zone: any): void {
    if (!this.campDraft && this.camp) {
      this.campDraft = structuredClone(this.camp);
    }
    if (this.campDraft) {
      this.campDraft.delimitation = {
        name: zone.name ?? 'delim',
        color: zone.color ?? '#ff0000',
        isCampDelimitation: zone.isCampDelimitation ?? true,
        zone: zone.feature ?? zone,
      };
      this.camp = { ...(this.camp ?? this.campDraft), delimitation: this.campDraft.delimitation } as FullCampDto;
      this.markDirty();
    }
    this.showDelimPicker = false;
  }

  cancelLocationPicker(): void {
    this.showLocationPicker = false;
  }

  cancelDelimPicker(): void {
    this.showDelimPicker = false;
  }

  closeFeaturePopup(): void {
    this.showFeaturePopup = false;
    this.selectedFeature = null;
    this.featureDraft = null;
  }

  saveFeatureDraft(): void {
    if (!this.featureDraft) return;
    // Update list
    const idx = this.features.findIndex((f) => f.id === this.featureDraft!.id);
    if (idx >= 0) {
      this.features = [
        ...this.features.slice(0, idx),
        structuredClone(this.featureDraft),
        ...this.features.slice(idx + 1),
      ];
    } else {
      this.features = [...this.features, structuredClone(this.featureDraft)];
    }
    this.markDirty();
    this.showFeaturePopup = false;
  }

  deleteFeatureDraft(): void {
    if (!this.featureDraft) return;
    const id = this.featureDraft.id;
    this.features = this.features.filter((f) => f.id !== id);
    if (id > 0) {
      this.deletedFeatureIds.push(id);
    }
    this.markDirty();
    this.closeFeaturePopup();
  }

  private pushUndo(): void {
    this.undoStack.push(this.currentSnapshot());
  }

  private clearRedo(): void {
    this.redoStack = [];
  }

  private currentSnapshot(): { camp: FullCampDto | null; features: FeatureDto[] } {
    return {
      camp: this.camp ? structuredClone(this.camp) : null,
      features: structuredClone(this.features),
    };
  }

  private applySnapshot(snapshot: { camp: FullCampDto | null; features: FeatureDto[] }): void {
    this.camp = snapshot.camp ? structuredClone(snapshot.camp) : null;
    this.features = structuredClone(snapshot.features);
    this.dirty = !this.isSameAsSaved(snapshot);
  }

  private setSavedSnapshot(): void {
    this.lastSavedSnapshot = this.currentSnapshot();
    this.dirty = false;
  }

  private isSameAsSaved(snapshot: { camp: FullCampDto | null; features: FeatureDto[] }): boolean {
    if (!this.lastSavedSnapshot) return false;
    return (
      JSON.stringify(snapshot.camp) === JSON.stringify(this.lastSavedSnapshot.camp) &&
      JSON.stringify(snapshot.features) === JSON.stringify(this.lastSavedSnapshot.features)
    );
  }

  private markDirty(): void {
    this.dirty = true;
  }

  private getChangedExistingFeatures(): FeatureDto[] {
    if (!this.lastSavedSnapshot) return [];
    const savedMap = new Map<number, FeatureDto>();
    (this.lastSavedSnapshot.features ?? []).forEach((f) => savedMap.set(f.id, f));

    return this.features.filter((f) => {
      if (f.id <= 0) return false;
      const saved = savedMap.get(f.id);
      if (!saved) return true;
      return JSON.stringify(f) !== JSON.stringify(saved);
    });
  }
}
