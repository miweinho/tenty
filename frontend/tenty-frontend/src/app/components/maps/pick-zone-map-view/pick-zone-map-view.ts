// src/app/components/camp-map/pick-location-map-view.component.ts
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Polygon from 'ol/geom/Polygon';

import { MapComponent } from '../../pages/map/map';
import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { GeoJsonFeatureDto } from '../../../models';

import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-pick-zone-map-view',
  imports: [
    CommonModule,
    MapComponent,
    MatButtonModule,
    MatToolbarModule,
    MatIconModule
  ],
  templateUrl: './pick-zone-map-view.html',
  styleUrls: ['./pick-zone-map-view.css'],
})
export class PickZoneMapView extends BaseCampMapView {
  /**
   * Optional: initial location (e.g. when editing an existing camp).
   */
  @Input() initialZone: GeoJsonFeatureDto | null = null;

  /**
   * Emits the picked location as GeoJSON Feature when user confirms.
   */
  @Output() zonePicked = new EventEmitter<GeoJsonFeatureDto>();

  /**
   * Emits when the user cancels the picking (optional).
   */
  @Output() cancel = new EventEmitter<void>();

  private selectedFeature: Feature<Polygon> | null = null;
  private isPicking = false;

  protected override afterMapInitialized(_map: Map): void {
    // Beim Laden: alte Features wegräumen
    this.drawingService.clearAll();

    // Falls ein initialer Standort vorhanden ist -> einzeichnen
    if (this.initialZone) {
      const feature = this.campMapHelper.featureFromDto(
        this.initialZone,
        { kind: 'picked-zone' }
      ) as Feature<Polygon>;

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
   * Startet den "Zone wählen"-Modus:
   * Der nächste Klick auf die Karte erzeugt genau EIN Point-Feature
   * über den DrawingService.
   */
  async startPickingZone(): Promise<void> {
    if (!this.map) return;

    this.isPicking = true;

    // Vorherige Auswahl entfernen
    if (this.selectedFeature) {
      this.drawingService.removeFeature(this.selectedFeature);
      this.selectedFeature = null;
    }

    // Wir verwenden den DrawingService, um den Punkt zu setzen.
    // Variante 1: existierende API waitForClickAndCreatePoint()
    const feature = await this.drawingService.waitForClickAndCreatePolygon();

    this.isPicking = false;

    if (!feature) {
      return;
    }

    // Nur als Point verwenden
    this.selectedFeature = feature as Feature<Polygon>;

    // Optional: etwas reinzoomen / zentrieren
    this.fitViewToAllFeatures();
  }

  /**
   * User klickt auf "Standort übernehmen".
   * -> GeoJSON-DTO erzeugen und an Parent emittieren.
   */
  confirmZone(): void {
    if (!this.selectedFeature) return;

    const dto = this.campMapHelper.dtoFromFeature(this.selectedFeature);
    this.zonePicked.emit(dto);
  }

  clearZone(): void {
    if (this.selectedFeature) {
      this.drawingService.removeFeature(this.selectedFeature);
      this.selectedFeature = null;
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }
}
