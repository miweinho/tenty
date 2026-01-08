import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import { Vector as VectorLayer } from 'ol/layer';
import { Vector as VectorSource } from 'ol/source';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';
import Draw from 'ol/interaction/Draw';
import { Type as GeometryType } from 'ol/geom/Geometry';
import { createEmpty, extend as extendExtent, getWidth, getHeight } from 'ol/extent';

import { MapStyleService } from '../mapStyle/map-style.service';

@Injectable({
  providedIn: 'root'
})
export class DrawingService {
  private map?: Map;
  private vectorSource = new VectorSource<Feature<Geometry>>();

  // vorher: private vectorLayer = new VectorLayer({ ... style: this.mapStyle.layerStyleFn })
  private vectorLayer!: VectorLayer<VectorSource<Feature<Geometry>>>;

  constructor(private readonly mapStyle: MapStyleService) {
    this.vectorSource.on('addfeature', () => this.updateFeatures());

    this.vectorLayer = new VectorLayer({
      source: this.vectorSource,
      style: this.mapStyle.layerStyleFn,
    });

    // Alles auf diesem Layer (Fill, Stroke, Text, Icons) 50% transparent
    this.vectorLayer.setOpacity(0.5);
  }

  // Observables
  private featuresSubject = new BehaviorSubject<Feature<Geometry>[]>([]);
  public readonly features$ = this.featuresSubject.asObservable();

  private featureClickSubject = new Subject<Feature<Geometry>>();
  public readonly featureClick$ = this.featureClickSubject.asObservable();

  // Draw interaction
  private draw?: Draw;

  /**
   * Initialisiert den Service mit einer Map-Instanz
   */
  initializeMap(map: Map): void {
    // Falls Layer bereits auf anderer Map, entfernen
    if (this.map && this.map !== map) {
      this.map.removeLayer(this.vectorLayer);
    }

    this.map = map;
    this.map.addLayer(this.vectorLayer);

    // Click-Listener für Features einrichten
    this.setupClickListener();
  }

  /**
   * Richtet Click-Listener für Features ein
   */
  private setupClickListener(): void {
    if (!this.map) return;

    this.map.on('click', (evt) => {
      const feature = this.map?.forEachFeatureAtPixel(
        evt.pixel,
        (f) => f as Feature<Geometry>
      );

      if (feature) {
        this.featureClickSubject.next(feature);
      }
    });
  }

  // ---------------------------------------------------
  // Feature-Management
  // ---------------------------------------------------

  /**
   * Fügt ein Feature hinzu
   */
  addFeature(feature: Feature<Geometry>): void {
    this.vectorSource.addFeature(feature);
  }

  /**
   * Fügt ein Feature mit zusätzlichen Metadaten hinzu
   */
  addFeatureWithMetadata(feature: Feature<Geometry>, metadata?: Record<string, any>): void {
    if (metadata) {
      Object.entries(metadata).forEach(([key, value]) => {
        feature.set(key, value);
      });
    }
    this.addFeature(feature);
  }

  /**
   * Entfernt ein Feature
   */
  removeFeature(feature: Feature<Geometry>): void {
    this.vectorSource.removeFeature(feature);
  }

  /**
   * Gibt alle Features zurück
   */
  getAllFeatures(): Feature<Geometry>[] {
    return this.vectorSource.getFeatures();
  }

  /**
   * Löscht alle Features
   */
  clearAll(): void {
    this.vectorSource.clear();
  }

  // ---------------------------------------------------
  // Draw-Interaktion
  // ---------------------------------------------------

  /**
   * Zeichnet ein einzelnes Feature und gibt es zurück
   * @param type Geometrie-Typ (z.B. 'Polygon', 'Point', 'LineString')
   * @returns Promise mit dem gezeichneten Feature
   */
  drawOnce(type: GeometryType): Promise<Feature<Geometry>> {
    return new Promise((resolve, reject) => {
      if (!this.map) {
        reject(new Error('Map nicht initialisiert'));
        return;
      }

      // Altes Draw entfernen falls vorhanden
      if (this.draw) {
        this.map.removeInteraction(this.draw);
      }

      // Neues Draw erstellen
      this.draw = new Draw({
        source: this.vectorSource,
        type: type,
      });

      // Bei drawend: Feature zurückgeben und Draw entfernen
      this.draw.on('drawend', (event) => {
        const feature = event.feature as Feature<Geometry>;
        
        // Draw entfernen nach dem Zeichnen
        setTimeout(() => {
          if (this.draw && this.map) {
            this.map.removeInteraction(this.draw);
            this.draw = undefined;
          }
        }, 0);

        resolve(feature);
      });

      this.map.addInteraction(this.draw);
    });
  }

  /**
   * Startet den Zeichnungsmodus
   * @param type Geometrie-Typ
   */
  startDrawing(type: GeometryType): void {
    if (!this.map) return;

    // Altes Draw entfernen
    this.stopDrawing();

    this.draw = new Draw({
      source: this.vectorSource,
      type: type,
    });

    this.map.addInteraction(this.draw);
  }

  /**
   * Stoppt den Zeichnungsmodus
   */
  stopDrawing(): void {
    if (this.draw && this.map) {
      this.map.removeInteraction(this.draw);
      this.draw = undefined;
    }
  }

  // ---------------------------------------------------
  // View Management
  // ---------------------------------------------------

  /**
   * Passt die Kartenansicht an alle Features an
   */
  fitViewToAllFeatures(): void {
    if (!this.map) return;
    const features = this.getAllFeatures();
    if (!features.length) return;

    const extent = createEmpty();
    features.forEach((f) => {
      const geom = f.getGeometry();
      if (geom) {
        const e = geom.getExtent();
        extendExtent(extent, e);
      }
    });

    const width = getWidth(extent);
    const height = getHeight(extent);
    const maxDim = Math.max(width, height);

    // Center berechnen
    const center = [
      (extent[0] + extent[2]) / 2,
      (extent[1] + extent[3]) / 2,
    ];

    const view = this.map.getView();

    // Zoom-Level basierend auf Ausdehnung
    let zoom: number;
    if (maxDim < 200) {
      zoom = 18;
    } else if (maxDim < 1000) {
      zoom = 16;
    } else if (maxDim < 5000) {
      zoom = 14;
    } else if (maxDim < 20000) {
      zoom = 10;
    } else {
      zoom = 8;
    }

    view.setCenter(center);
    view.setZoom(zoom);
  }

  // ---------------------------------------------------
  // Observable-Update
  // ---------------------------------------------------

  private updateFeatures(): void {
    this.featuresSubject.next(this.getAllFeatures());
  }
}
