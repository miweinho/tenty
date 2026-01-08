// base-camp-map-view.ts
import { inject } from '@angular/core';
import Map from 'ol/Map';

import { DrawingService } from '../../../service/drawingService/drawing-service';
import { CampMapHelperService } from '../../../service/campMapHelperService/camp-map-helper';

export abstract class BaseCampMapView {
  protected drawingService = inject(DrawingService);
  protected campMapHelper = inject(CampMapHelperService);

  protected map: Map | null = null;

  onMapReady(map: Map): void {
    this.map = map;
    this.drawingService.initializeMap(map);
    this.afterMapInitialized(map);
  }

  protected afterMapInitialized(_map: Map): void {
    // Default: nichts
  }

  /**
   * Setzt Center & Zoom basierend auf allen Features im DrawingService.
   */
  protected fitViewToAllFeatures(): void {
    this.drawingService.fitViewToAllFeatures();
  }
}
