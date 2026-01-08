import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { Subject, takeUntil, forkJoin, finalize, take, firstValueFrom } from 'rxjs';

import { CampMainMapView } from '../../../maps/camp-main-map-view/camp-main-map-view';
import { MapToolbarComponent } from '../../../maps/map-toolbar/map-toolbar';

import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { CampService } from '../../../../service/camps/camp';
import { FeatureService } from '../../../../service/feature/feature.service';

import { FeatureDto, FullCampDto } from '../../../../models';

@Component({
  selector: 'app-main-camp-view',
  standalone: true,
  imports: [
    CommonModule,
    CampMainMapView,
    MapToolbarComponent,
    MatButtonModule,
    MatIconModule,
    MatToolbarModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './main-camp-view.html',
  styleUrls: ['./main-camp-view.css'],
})
export class MainCampViewComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private campService = inject(CampService);
  private featureService = inject(FeatureService);
  private cdr = inject(ChangeDetectorRef);
  private destroyed = false;

  private destroy$ = new Subject<void>();

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
      if(!this.destroyed) this.cdr.detectChanges();
    }
  }

  // ---- Toolbar actions
  onRefresh(): void {
    if (this.camp?.id) {
      this.loadCamp(this.camp.id);
    }
  }

  onDrawTent(): void {
    // Map component kümmert sich darum, wird dort aufgerufen
  }

  onSave(): void {
    // TODO: implement save
    console.log('Save clicked');
  }
}
