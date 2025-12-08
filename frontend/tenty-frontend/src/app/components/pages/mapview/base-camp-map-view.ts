// base-camp-map-view.ts
import { inject } from '@angular/core';
import Map from 'ol/Map';
import {
  createEmpty,
  extend as extendExtent,
  getWidth,
  getHeight,
} from 'ol/extent';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';

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
   * Kein view.fit, sondern eigene Heuristik:
   *  - extents zusammenfassen
   *  - Center = Mitte des Extents
   *  - Zoom abhängig von der Größe des Extents (in Metern, EPSG:3857)
   */
  protected fitViewToAllFeatures(): void {
    if (!this.map) return;
    const features: Feature<Geometry>[] = this.drawingService.getAllFeatures();
    if (!features.length) return;

    const extent = createEmpty();
    features.forEach((f, index) => {
      const geom = f.getGeometry();
      if (geom) {
        const e = geom.getExtent();
        extendExtent(extent, e);
      }
    });

    const width = getWidth(extent);   // in Metern (EPSG:3857)
    const height = getHeight(extent);
    const maxDim = Math.max(width, height);

    // Center berechnen
    const center = [
      (extent[0] + extent[2]) / 2,
      (extent[1] + extent[3]) / 2,
    ];

    const view = this.map.getView();

    // Heuristik für Zoom (anpassbar)
    // Werte kannst du später feinjustieren:
    let zoom: number;
    if (maxDim < 200) {
      zoom = 18; // sehr nahe beieinander, stark reinzoomen
    } else if (maxDim < 1000) {
      zoom = 17; // ~1 km -> Stadtteil
    } else if (maxDim < 5000) {
      zoom = 16; // einige km
    } else if (maxDim < 20000) {
      zoom = 15; // Stadt / Region
    } else {
      zoom = 13; // weiter auseinander -> Region
    }

    view.setCenter(center);
    view.setZoom(zoom);
  }
}
