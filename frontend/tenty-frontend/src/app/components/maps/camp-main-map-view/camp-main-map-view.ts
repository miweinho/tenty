import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';

import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { MapComponent } from '../../pages/map/map';
import { CampMapHelperService } from '../../../service/campMapHelperService/camp-map-helper';
import { DrawingService } from '../../../service/drawingService/drawing-service';
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

  @Output() featureSelected = new EventEmitter<FeatureDto>();
  @Output() mapReadyEvent = new EventEmitter<Map>();

  protected override afterMapInitialized(map: Map): void {
    this.map = map;
    this.redraw();

    // Bubble map ready
    this.mapReadyEvent.emit(map);

    // Handle feature clicks
    this.drawingService.featureClick$.subscribe((f) => {
      const props = f.getProperties() as any;
      const dto = props['featureDto'] as FeatureDto | undefined;
      if (dto) {
        this.featureSelected.emit(dto);
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['camp'] || changes['features']) {
      this.redraw();
    }
  }

  private redraw(): void {
    if (!this.map) return;
    this.drawingService.clearAll();

    // Camp location
    if (this.camp?.location) {
      const campName = this.camp.name ?? (this.camp as any)?.campName ?? 'Camp';
      const loc = this.campMapHelper.featureFromDto(this.camp.location, {
        kind: 'camp-location',
        name: campName,
      });
      this.campMapHelper.applyCampLabelStyle(loc, campName);
      this.drawingService.addFeature(loc);
    }

    // Camp delimitation
    const delim = (this.camp as any)?.delimitation ?? (this.camp as any)?.Delimitation;
    if (delim?.zone) {
      const delimFeature = this.campMapHelper.featureFromDto(delim.zone, {
        kind: 'camp-delimitation',
        color: delim.color,
        name: delim.name,
      });
      this.drawingService.addFeature(delimFeature);
    }

    // Features
    (this.features ?? []).forEach((f) => {
      const feature = this.campMapHelper.featureFromDto(f.geometry, {
        kind: f.kind,
        name: f.name,
        color: f.color,
      });
      this.drawingService.addFeatureWithMetadata(feature as Feature<Geometry>, {
        featureDto: f,
      });
    });

    // size update + fit to all features
    try {
      this.map.updateSize();
    } catch (e) {
      // ignore
    }
    this.fitViewToAllFeatures();
    setTimeout(() => this.fitViewToAllFeatures(), 50);
  }
}
