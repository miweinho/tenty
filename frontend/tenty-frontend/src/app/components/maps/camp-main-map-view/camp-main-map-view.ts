import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';
import Point from 'ol/geom/Point';

import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { MapComponent } from '../../pages/map/map';

import { CreateFeatureDto, FeatureDto, FullCampDto, GeoJsonFeatureDto } from '../../../models';

@Component({
  selector: 'app-camp-main-map-view',
  standalone: true,
  imports: [CommonModule, MapComponent],
  templateUrl: './camp-main-map-view.html',
  styleUrls: ['./camp-main-map-view.css'],
})
export class CampMainMapView extends BaseCampMapView implements OnChanges {
  @Input() camp: FullCampDto | null = null;
  @Input() features: FeatureDto[] | null = null;
  @Output() objectPlaced = new EventEmitter<{ x: number; y: number }>();

  private placementClickKey: any = null;

  ngOnChanges(changes: SimpleChanges): void {
    // Nur redraw wenn map ready ist UND Daten sich ändern
    if ((changes['camp'] || changes['features']) && this.map) {
      this.redraw();
      this.drawingService.fitViewToAllFeatures();
    }
  }

  protected override afterMapInitialized(map: Map): void {
    this.map = map;
    this.drawingService.clearAll();

    // Falls Daten beim Map-Init schon vorhanden sind
    if (this.camp || this.features?.length) {
      this.redraw();
      this.drawingService.fitViewToAllFeatures();
    }
  }

  private redraw(): void {
    if (!this.map) return;

    // WICHTIG: Alles löschen
    this.drawingService.clearAll();

    console.log('[CampMainMapView] redraw camp:', this.camp?.id, 'features:', this.features?.length);

    // 1. Camp location zeichnen
    if (this.camp?.location) {
      const campName = this.camp.name ?? 'Camp';
      const locationFeature = this.campMapHelper.featureFromDto(this.camp.location, {
        kind: 'camp-location',
        name: campName,
      });
      this.campMapHelper.applyCampLabelStyle(locationFeature, campName);
      this.drawingService.addFeature(locationFeature);
    }

    // 2. Delimitation zeichnen
    const delim = (this.camp as any)?.delimitation ?? (this.camp as any)?.Delimitation;
    if (delim?.zone) {
      const delimFeature = this.campMapHelper.featureFromDto(delim.zone, {
        kind: 'camp-delimitation',
        color: delim.color,
        name: delim.name,
      });
      this.drawingService.addFeature(delimFeature);
    }

    // 3. Features zeichnen
    console.log(this.features);
    (this.features ?? []).forEach((f) => {
      const olFeature = this.campMapHelper.featureFromDto(f.geometry, {
        id: f.id,
        campId: f.campId,
        kind: f.kind,
        name: f.name,
        color: f.color,
        lengthM: f.lengthM ?? 40,
        widthM: f.widthM ?? 20,
        headingDeg: f.headingDeg ?? 0,
        icon: f.icon,
      });

      // Metadata speichern für Click-Handling später
      olFeature.set('featureDto', f);

      this.drawingService.addFeature(olFeature as Feature<Geometry>);
    });

    try {
      this.map.updateSize();
    } catch {}
  }

  /**
   * Startet den Objektplatzierungs-Modus
   * Wartet auf einen Klick auf der Karte und emittiert die Position
   */
  async startObjectPlacement(): Promise<GeoJsonFeatureDto> {
    let geometry = await this.drawingService.drawOnce('Point')
    console.log("geometry placed: ", geometry);
    return this.campMapHelper.dtoFromFeature(geometry) as GeoJsonFeatureDto;
  }

  cancelObjectPlacement(): void {
    this.drawingService.stopDrawing();
  }
}
