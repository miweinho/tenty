import { Component, Input, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';

import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { MapComponent } from '../../pages/map/map';

import { FeatureDto, FullCampDto } from '../../../models';

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
}
