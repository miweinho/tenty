import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef, ViewChild, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { Subject, takeUntil, forkJoin, finalize, take, firstValueFrom } from 'rxjs';

import { CampMainMapView } from '../../../maps/camp-main-map-view/camp-main-map-view';
import { MapToolbarComponent } from '../../../maps/map-toolbar/map-toolbar';
import { Feature } from '../../../forms/feature/feature'; // Importiere Feature Component

import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { CampService } from '../../../../service/camps/camp';
import { FeatureService } from '../../../../service/feature/feature.service';

import { CreateFeatureDto, FeatureDto, FeatureKind, FullCampDto, GeoJsonFeatureDto } from '../../../../models';
import { MatChip } from '@angular/material/chips';

@Component({
  selector: 'app-main-camp-view',
  standalone: true,
  imports: [
    CommonModule,
    CampMainMapView,
    MapToolbarComponent,
    Feature, // Importiere hier die Feature Komponente
    MatButtonModule,
    MatIconModule,
    MatToolbarModule,
    MatProgressSpinnerModule,
    MatChip,
  ],
  templateUrl: './main-camp-view.html',
  styleUrls: ['./main-camp-view.css'],
})
export class MainCampViewComponent implements OnInit, OnDestroy {
  @ViewChild(CampMainMapView) mapView!: CampMainMapView;

  // UI state for creation
  showFeatureForm = false;
  selectedFeatureType: string | null = null;

  private route = inject(ActivatedRoute);
  private campService = inject(CampService);
  private featureService = inject(FeatureService);
  private cdr = inject(ChangeDetectorRef);
  private destroyed = false;
  private emptyDto: GeoJsonFeatureDto | null = null;

  private destroy$ = new Subject<void>();
  private zone = inject(NgZone);

  // ---- State
  camp: FullCampDto | null = null;
  features: FeatureDto[] = [];
  loading = false;
  error: string | null = null;

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe((params) => {
      const id = Number(params.get('id'));
      if (!Number.isNaN(id)) {
        this.loadCamp(id);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ---- Load camp + features
  private async loadCamp(campId: number): Promise<void> {
    this.loading = true;
    this.error = null;

    try {
      const [camp, features] = await Promise.all([
        firstValueFrom(this.campService.loadCamp(campId, true)),
        firstValueFrom(this.featureService.loadFeaturesForCamp(campId)),
      ]);

      this.camp = camp;
      console.log(camp);
      this.features = features ?? [];
      console.log(features);
    } catch (err) {
      console.error('Load failed:', err);
      this.error = 'Failed to load camp';
    } finally {
      this.loading = false;
      if (!this.destroyed) this.cdr.detectChanges();
    }
  }

  // ---- Toolbar actions
  onRefresh(): void {
    if (this.camp?.id) {
      console.log('refresh works');
      this.loadCamp(this.camp.id);
    }
  }

  onSave(): void {}

  async onDrawLine(): Promise<void> {
    this.selectedFeatureType = 'water';
    this.showFeatureForm = false;

    this.cdr.detectChanges();
    const dto = await this.mapView.startLinePlacement();

    this.zone.run(()=> {
      this.emptyDto = dto;
      this.showFeatureForm = true;
      this.cdr.markForCheck();
    })
    
  }

  async onDrawObject(): Promise<void> {
    this.selectedFeatureType = 'tent';
    this.showFeatureForm = false;

    // optional: UI sofort aktualisieren, falls du vorher etwas ausblendest
    this.cdr.detectChanges();

    const dto = await this.mapView.startObjectPlacement();

    // Wichtig: wieder in Angular-Zone, damit *ngIf etc. aktualisiert
    this.zone.run(() => {
      this.emptyDto = dto;
      this.showFeatureForm = true;

      // falls OnPush oder Zone-Edgecases: explizit
      this.cdr.markForCheck(); // oder detectChanges()
    });
  }

  closeFeatureForm(): void {
    this.zone.run(() => {
      this.showFeatureForm = false;
      this.selectedFeatureType = null;
      this.cdr.markForCheck();
    });

    this.mapView?.cancelObjectPlacement();
  }

  async onFeatureCreated(payload: {
    kind: 'tent';
    name: string;
    lengthM: number;
    widthM: number;
    headingDeg: number;
    color: string;
  }): Promise<void> {
    if (!this.camp?.id || this.emptyDto === null) return;

    const dto: CreateFeatureDto = {
      campId: this.camp.id,
      kind: payload.kind,
      name: payload.name,
      color: payload.color,
      lengthM: payload.lengthM,
      widthM: payload.widthM,
      headingDeg: payload.headingDeg,
      geometry: this.emptyDto,
      icon: payload.kind,
    };

    try {
      this.loading = true;

      // Backend speichern
      const created = await firstValueFrom(this.featureService.createFeature(dto));

      // UI aktualisieren (entweder push oder komplett neu laden)
      this.features = [created, ...this.features];

      // Form schließen + Zeichnen abbrechen/cleanup
      this.closeFeatureForm();
      this.emptyDto = null;
    } catch (e) {
      console.error('Create feature failed', e);
      this.error = 'Failed to create feature';
    } finally {
      this.loading = false;
      this.cdr.detectChanges();
    }
  }
}
