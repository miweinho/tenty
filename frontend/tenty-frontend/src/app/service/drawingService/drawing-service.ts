import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject, Observable } from 'rxjs';
import { Vector as VectorLayer } from 'ol/layer';
import { Vector as VectorSource } from 'ol/source';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';
import { Style, Fill, Stroke, Circle, Text } from 'ol/style';
import { FeatureLike } from 'ol/Feature';
import Draw from 'ol/interaction/Draw';
import { Type as GeometryType } from 'ol/geom/Geometry';
import {
  createEmpty,
  extend as extendExtent,
  getWidth,
  getHeight,
} from 'ol/extent';

@Injectable({
  providedIn: 'root'
})
export class DrawingService {
  private map?: Map;
  private vectorSource = new VectorSource<Feature<Geometry>>();
  private vectorLayer = new VectorLayer({
    source: this.vectorSource,
    style: (feature: FeatureLike) => this.getStyleForFeature(feature)
  });

  // Observables
  private featuresSubject = new BehaviorSubject<Feature<Geometry>[]>([]);
  public readonly features$ = this.featuresSubject.asObservable();

  private featureClickSubject = new Subject<Feature<Geometry>>();
  public readonly featureClick$ = this.featureClickSubject.asObservable();

  // Draw interaction
  private draw?: Draw;

  constructor() {
    // Listen to feature changes
    this.vectorSource.on('addfeature', () => this.updateFeatures());
    this.vectorSource.on('removefeature', () => this.updateFeatures());
    this.vectorSource.on('changefeature', () => this.updateFeatures());
    this.vectorSource.on('clear', () => this.updateFeatures());
  }

  // ---------------------------------------------------
  // Map Initialisierung
  // ---------------------------------------------------

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
  // Styling
  // ---------------------------------------------------

  /**
   * Gibt den Style für ein Feature basierend auf seinen Properties zurück
   * - Wendet Farbe und Namen für alle kinds an
   * - Für "tent": zusätzlich Länge und Breite
   */
  private getStyleForFeature(feature: FeatureLike): Style {
    const color = feature.get('color') ?? '#ffcc33';
    const name = feature.get('name') ?? '';
    const kind = feature.get('kind');

    // Spezielle Behandlung für verschiedene Typen
    switch (kind) {
      case 'tent':
        return this.getTentStyle(feature, color, name);
      case 'camp-location':
        return this.getCampLocationStyle(color, name);
      case 'camp-delimitation':
        return this.getDelimitationStyle(color, name);
      default:
        return this.getDefaultFeatureStyle(color, name);
    }
  }

  /**
   * Style für Zelte mit Länge und Breite
   */
  private getTentStyle(feature: FeatureLike, color: string, name: string): Style {
    const lengthM = feature.get('lengthM') ?? 40;
    const widthM = feature.get('widthM') ?? 20;

    let labelText = name;
    if (name) {
      labelText += `\n${lengthM}m × ${widthM}m`;
    } else {
      labelText = `${lengthM}m × ${widthM}m`;
    }

    return new Style({
      fill: new Fill({
        color: this.hexToRgba(color, 0.3),
      }),
      stroke: new Stroke({
        color: color,
        width: 3,
      }),
      text: new Text({
        text: labelText,
        font: 'bold 12px Arial, sans-serif',
        fill: new Fill({
          color: '#000000',
        }),
        stroke: new Stroke({
          color: '#ffffff',
          width: 3,
        }),
        offsetY: -20,
      }),
    });
  }

  /**
   * Style für Camp-Position (Marker)
   */
  private getCampLocationStyle(color: string, name: string): Style {
    return new Style({
      image: new Circle({
        radius: 10,
        fill: new Fill({
          color: color,
        }),
        stroke: new Stroke({
          color: '#ffffff',
          width: 2,
        }),
      }),
      text: new Text({
        text: name,
        font: 'bold 14px Arial, sans-serif',
        fill: new Fill({
          color: '#000000',
        }),
        stroke: new Stroke({
          color: '#ffffff',
          width: 3,
        }),
        offsetY: -20,
      }),
    });
  }

  /**
   * Style für Delimitation (Abgrenzung)
   */
  private getDelimitationStyle(color: string, name: string): Style {
    return new Style({
      fill: new Fill({
        color: this.hexToRgba(color, 0.1),
      }),
      stroke: new Stroke({
        color: color,
        width: 2,
        lineDash: [10, 5], // Gestrichelte Linie
      }),
      text: new Text({
        text: name,
        font: '12px Arial, sans-serif',
        fill: new Fill({
          color: '#000000',
        }),
        stroke: new Stroke({
          color: '#ffffff',
          width: 3,
        }),
        offsetY: -15,
      }),
    });
  }

  /**
   * Standard-Style für alle anderen Features
   */
  private getDefaultFeatureStyle(color: string, name: string): Style {
    return new Style({
      fill: new Fill({
        color: this.hexToRgba(color, 0.2),
      }),
      stroke: new Stroke({
        color: color,
        width: 2,
      }),
      image: new Circle({
        radius: 7,
        fill: new Fill({
          color: color,
        }),
      }),
      text: new Text({
        text: name,
        font: '12px Arial, sans-serif',
        fill: new Fill({
          color: '#000000',
        }),
        stroke: new Stroke({
          color: '#ffffff',
          width: 3,
        }),
        offsetY: -15,
      }),
    });
  }

  /**
   * Konvertiert Hex-Farbe zu RGBA mit Alpha
   * Input: "#abcabc", alpha: 0.2
   * Output: "rgba(171, 202, 188, 0.2)"
   */
  private hexToRgba(hex: string, alpha: number): string {
    const cleanHex = hex.replace('#', '');

    if (cleanHex.length !== 6) {
      return `rgba(255, 204, 51, ${alpha})`; // Fallback
    }

    const r = parseInt(cleanHex.substring(0, 2), 16);
    const g = parseInt(cleanHex.substring(2, 4), 16);
    const b = parseInt(cleanHex.substring(4, 6), 16);

    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
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
