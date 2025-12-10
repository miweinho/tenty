// src/app/components/camp-map/pick-location-map-view.component.ts
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import { Point } from 'ol/geom';

import { MapComponent } from '../../pages/map/map';
import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { GeoJsonFeatureDto } from '../../../models';

import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-pick-location-map-view',
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
export class PickLocationMapViewComponent extends BaseCampMapView {
  /**
   * Optional: initial location (e.g. when editing an existing camp).
   */
  @Input() initialLocation: GeoJsonFeatureDto | null = null;

  /**
   * Emits the picked location as GeoJSON Feature when user confirms.
   */
  @Output() locationConfirm = new EventEmitter<GeoJsonFeatureDto>();

  /**
   * Emits when the user cancels the picking (optional).
   */
  @Output() cancel = new EventEmitter<void>();

  private selectedFeature: Feature<Point> | null = null;
  private isPicking = false;

  protected override afterMapInitialized(_map: Map): void {
    this.map = _map;
    // Beim Laden: alte Features wegräumen
    this.drawingService.clearAll();

    // Falls ein initialer Standort vorhanden ist -> einzeichnen
    if (this.initialLocation) {
      const feature = this.campMapHelper.featureFromDto(
        this.initialLocation,
        { kind: 'picked-location' }
      ) as Feature<Point>;

      // Einfach denselben Style wie Default verwenden oder hier speziellen Marker setzen
      // (optional könntest du einen eigenen Marker-Style dafür definieren)
      this.drawingService.addFeature(feature);
      this.selectedFeature = feature;

      // Auf den Punkt zoomen
      this.fitViewToAllFeatures();
    }
  }

  get hasSelection(): boolean {
    return !!this.selectedFeature;
  }

  get pickingActive(): boolean {
    return this.isPicking;
  }

  /**
   * Startet den "Standort wählen"-Modus:
   * Der nächste Klick auf die Karte erzeugt genau EIN Point-Feature
   * über den DrawingService.
   */
  async startPickingLocation(): Promise<void> {
    
    if (!this.map) return;
    console.log("start picking locaiton");

    this.isPicking = true;

    // Vorherige Auswahl entfernen
    if (this.selectedFeature) {
      this.drawingService.removeFeature(this.selectedFeature);
      this.selectedFeature = null;
    }

    // Wir verwenden den DrawingService, um den Punkt zu setzen.
    // Variante 1: existierende API waitForClickAndCreatePoint()
    const feature = await this.drawingService.waitForClickAndCreatePoint();

    this.isPicking = false;

    if (!feature) {
      return;
    }

    // Nur als Point verwenden
    this.selectedFeature = feature as Feature<Point>;

    // Optional: etwas reinzoomen / zentrieren
    this.fitViewToAllFeatures();
  }

  /**
   * User klickt auf "Standort übernehmen".
   * -> GeoJSON-DTO erzeugen und an Parent emittieren.
   */
  confirmSelection(): void {
    if (!this.selectedFeature) return;

    const dto = this.campMapHelper.dtoFromPointFeature(this.selectedFeature);
    this.locationConfirm.emit(dto);
  }

  onCancel(): void {
    this.cancel.emit();
  }
}
