// src/app/components/camp-map/camp-overview-map-view.component.ts
import { Component, inject, OnDestroy, Input, OnChanges, SimpleChanges, Output, EventEmitter } from '@angular/core';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';
import { MapComponent } from '../../pages/map/map';
import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { CampService } from '../../../service/camps/camp';
import { CampDto } from '../../../models';
import { Subject, takeUntil } from 'rxjs';

@Component({
  selector: 'app-camp-overview-map-view',
  imports: [MapComponent],
  templateUrl: './camp-overview-map-view.html',
  styleUrls: ['./camp-overview-map-view.css'],
})
export class CampOverviewMapViewComponent extends BaseCampMapView implements OnDestroy, OnChanges {
  private campService = inject(CampService);
  private destroy$ = new Subject<void>();

  public camps$ = this.campService.camps$;
  public camps: CampDto[] = [];
  private mapReady = false;

  @Output() campSelected = new EventEmitter<CampDto>();

  @Input() set externalCamps(camps: CampDto[] | null) {
    if (camps) {
      this.camps = camps;
      this.redrawCamps();
    }
  }

  protected override afterMapInitialized(map: Map): void {
    this.mapReady = true;

    // subscribe zu camps$ und redraw bei Änderung
    this.campService.camps$.pipe(takeUntil(this.destroy$)).subscribe({
      next: (camps) => {
        if (camps && camps.length > 0) {
          this.camps = camps;
          this.redrawCamps();
        }
      },
      error: (err) => {
      },
    });

    // trigger load (falls noch nicht geladen)
    this.campService.loadCampList().subscribe();

    // Subscribe zu Feature-Clicks vom DrawingService
    this.drawingService.featureClick$.pipe(takeUntil(this.destroy$)).subscribe({
      next: (feature: Feature<Geometry>) => this.handleFeatureClick(feature),
      error: (err) => console.error('Feature click error', err)
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['externalCamps']) {
      if (this.mapReady) {
        this.redrawCamps();
      }
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private redrawCamps(): void {
    if (!this.map || !this.camps || this.camps.length === 0) {
      console.warn('Cannot redraw: map not ready or no camps');
      return;
    }

    // clear existing features (optional: implement clearAll in DrawingService)
    // this.drawingService.clearAll();

    this.camps.forEach((camp) => {
      // defensive: handle both 'location' and 'Location' property names (camelCase vs PascalCase)
      const locFeature = this.campMapHelper.featureFromDto(
        camp.location ?? (camp as any).Location,
        {
          id: camp.id,
          name: camp.campName ?? (camp as any).CampName,
        }
      );
      this.campMapHelper.applyCampLabelStyle(
        locFeature,
        camp.campName ?? (camp as any).CampName
      );

      // add camp-Objekt als metadata zur feature
      this.drawingService.addFeatureWithMetadata(locFeature, {
        campId: camp.id,
        campName: camp.campName,
        camp: camp // gesamtes Camp-Objekt
      });
    });

    this.fitViewToAllFeatures();
  }

  private handleFeatureClick(feature: Feature<Geometry>): void {
    // getProperties() gibt alle Properties zurück, aber camp ist nested
    const properties = feature.getProperties();
    
    // camp wurde als metadata property gespeichert
    const camp = properties['camp'] as CampDto | undefined;

    if (camp) {
      this.campSelected.emit(camp);
    }
  }
}
