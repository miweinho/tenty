import { Component, EventEmitter, Input, Output, ChangeDetectorRef, NgZone, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import { Point } from 'ol/geom';
import { unByKey } from 'ol/Observable';

import { MapComponent } from '../../pages/map/map';
import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { GeoJsonFeatureDto } from '../../../models';
import { DrawingService } from '../../../service/drawingService/drawing-service';

import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-pick-location-map-view',
  standalone: true,
  providers: [DrawingService],
  imports: [
    CommonModule,
    MapComponent,
    MatButtonModule,
    MatToolbarModule,
    MatIconModule
  ],
  templateUrl: './pick-location-map-view.html',
  styleUrls: ['./pick-location-map-view.css'],
})
export class PickLocationMapViewComponent extends BaseCampMapView implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  private ngZone = inject(NgZone);

  @Input() initialLocation: GeoJsonFeatureDto | null = null;

  @Output() locationConfirm = new EventEmitter<GeoJsonFeatureDto>();
  @Output() cancel = new EventEmitter<void>();

  private selectedFeature: Feature<Point> | null = null;
  private isPicking = false;
  private clickKey: any = null;

  ngOnInit(): void {
    this.isPicking = false;
  }

  protected override afterMapInitialized(_map: Map): void {
    this.map = _map;
    this.drawingService.clearAll();

    if (this.initialLocation) {
      const feature = this.campMapHelper.featureFromDto(
        this.initialLocation,
        { kind: 'picked-location' }
      ) as Feature<Point>;
      this.drawingService.addFeature(feature);
      this.selectedFeature = feature;
      this.fitViewToAllFeatures();
    }
  }

  get hasSelection(): boolean {
    return !!this.selectedFeature;
  }

  get pickingActive(): boolean {
    return this.isPicking;
  }

  async startPickingLocation(): Promise<void> {
    if (!this.map) return;

    this.ngZone.run(() => {
      this.isPicking = true;
      this.cdr.markForCheck();
    });

    if (this.selectedFeature) {
      this.drawingService.removeFeature(this.selectedFeature);
      this.selectedFeature = null;
    }

    if (this.clickKey) {
      unByKey(this.clickKey);
      this.clickKey = null;
    }

    this.clickKey = this.map.once('singleclick', (evt) => {
      const feature = new Feature(new Point(evt.coordinate));
      this.drawingService.addFeature(feature);

      this.ngZone.run(() => {
        this.isPicking = false;
        this.selectedFeature = feature as Feature<Point>;
        this.fitViewToAllFeatures();
        this.cdr.markForCheck();
      });
    });
  }

  confirmSelection(): void {
    if (!this.selectedFeature) return;
    const dto = this.campMapHelper.dtoFromPointFeature(this.selectedFeature);
    this.locationConfirm.emit(dto);
  }

  onCancel(): void {
    this.cancel.emit();
  }
}
