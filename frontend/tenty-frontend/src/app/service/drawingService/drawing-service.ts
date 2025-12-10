import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import { Draw, Modify, Select, Snap } from 'ol/interaction';
import { Vector as VectorLayer } from 'ol/layer';
import { Vector as VectorSource } from 'ol/source';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';
import { Point, Polygon } from 'ol/geom';
import { Style, Fill, Stroke, Circle } from 'ol/style';
import { DrawType } from '../../models';


@Injectable({
  providedIn: 'root'
})
export class DrawingService {
  private map: Map | null = null;

  private vectorSource = new VectorSource<Feature<Geometry>>();
  private vectorLayer = new VectorLayer({
    source: this.vectorSource,
    style: this.getDefaultStyle()
  });

  private drawInteraction: Draw | null = null;
  private modifyInteraction: Modify | null = null;
  private selectInteraction: Select | null = null;
  private snapInteraction: Snap | null = null;

  // Observable für Features
  private featuresSubject = new BehaviorSubject<Feature<Geometry>[]>([]);
  public readonly features$ = this.featuresSubject.asObservable();

  // feature click event
  private featureClickSubject = new Subject<Feature<Geometry>>();
  public readonly featureClick$ = this.featureClickSubject.asObservable();

  constructor() {
    // Listen to feature changes
    this.vectorSource.on('addfeature', () => this.updateFeatures());
    this.vectorSource.on('removefeature', () => this.updateFeatures());
    this.vectorSource.on('changefeature', () => this.updateFeatures());
    this.vectorSource.on('clear', () => this.updateFeatures());
  }

  /**
   * Initialisiert den Service mit einer Map-Instanz und fügt die Vector-Layer hinzu.
   * Kann mehrfach aufgerufen werden (z.B. bei neuem MapComponent), alte Map wird dann „vergessen“.
   */
  initializeMap(map: Map): void {
    // Falls die Layer bereits auf einer anderen Map hängt, dort entfernen
    if (this.map && this.map !== map) {
      this.map.removeLayer(this.vectorLayer);
      this.removeInteractions(this.map);
    }

    this.map = map;
    this.map.addLayer(this.vectorLayer);
    this.setupInteractions();
    this.setupClickListener(); // ADD THIS
  }

  private setupInteractions(): void {
    if (!this.map) return;

    // Modify
    this.modifyInteraction = new Modify({ source: this.vectorSource });
    this.map.addInteraction(this.modifyInteraction);

    // Select
    this.selectInteraction = new Select();
    this.map.addInteraction(this.selectInteraction);

    // Snap
    this.snapInteraction = new Snap({ source: this.vectorSource });
    this.map.addInteraction(this.snapInteraction);
  }

  private setupClickListener(): void {
    if (!this.map) return;

    // listen to map click events
    this.map.on('click', (event: any) => {
      // hit detection: check if user clicked on a feature
      const features: Feature<Geometry>[] = [];
      this.map!.forEachFeatureAtPixel(
        event.pixel,
        (feature: any) => {
          // nur Features aus unserem vectorLayer
          if (feature instanceof Feature) {
            features.push(feature);
          }
        },
        {
          layerFilter: (layer: any) => layer === this.vectorLayer
        }
      );

      if (features.length > 0) {
        const clickedFeature = features[0];
        this.featureClickSubject.next(clickedFeature);
      }
    });
  }

  private removeInteractions(map: Map): void {
    if (this.modifyInteraction) {
      map.removeInteraction(this.modifyInteraction);
      this.modifyInteraction = null;
    }
    if (this.selectInteraction) {
      map.removeInteraction(this.selectInteraction);
      this.selectInteraction = null;
    }
    if (this.snapInteraction) {
      map.removeInteraction(this.snapInteraction);
      this.snapInteraction = null;
    }
    if (this.drawInteraction) {
      map.removeInteraction(this.drawInteraction);
      this.drawInteraction = null;
    }
  }

  // ---------------------------------------------------
  // Zeichnen
  // ---------------------------------------------------

  startDrawing(type: DrawType): void {
    this.stopDrawing();
    if (!this.map) return;

    this.drawInteraction = new Draw({
      source: this.vectorSource,
      type: type
    });

    this.map.addInteraction(this.drawInteraction);
  }

  stopDrawing(): void {
    if (this.drawInteraction && this.map) {
      this.map.removeInteraction(this.drawInteraction);
      this.drawInteraction = null;
    }
  }

  /**
   * Convenience: starte einen Draw-Mode für genau ein Feature.
   * Resolve mit dem erzeugten Feature bei drawend, stoppt dann automatisch das Zeichnen.
   */
  drawOnce(type: DrawType): Promise<Feature<Geometry> | null> {
    return new Promise(resolve => {
      if (!this.map) {
        resolve(null);
        return;
      }

      this.stopDrawing();

      const draw = new Draw({
        source: this.vectorSource,
        type: type
      });

      const onDrawEnd = (evt: any) => {
        const feature = evt.feature as Feature<Geometry>;
        // Aufräumen
        draw.un('drawend', onDrawEnd);
        if (this.map) {
          this.map.removeInteraction(draw);
        }
        this.drawInteraction = null;
        resolve(feature);
      };

      draw.on('drawend', onDrawEnd);

      this.map.addInteraction(draw);
      this.drawInteraction = draw;
    });
  }

  // ---------------------------------------------------
  // Select / Modify Steuerung
  // ---------------------------------------------------

  enableModify(enabled: boolean): void {
    if (!this.map || !this.modifyInteraction) return;
    if (enabled) {
      this.map.addInteraction(this.modifyInteraction);
    } else {
      this.map.removeInteraction(this.modifyInteraction);
    }
  }

  enableSelect(enabled: boolean): void {
    if (!this.map || !this.selectInteraction) return;
    if (enabled) {
      this.map.addInteraction(this.selectInteraction);
    } else {
      this.map.removeInteraction(this.selectInteraction);
    }
  }

  getSelectedFeatures(): Feature<Geometry>[] {
    if (!this.selectInteraction) return [];
    const collection = this.selectInteraction.getFeatures();
    return collection.getArray();
  }

  clearSelection(): void {
    if (!this.selectInteraction) return;
    this.selectInteraction.getFeatures().clear();
  }

  // ---------------------------------------------------
  // Feature-Management
  // ---------------------------------------------------

  clearAll(): void {
    this.vectorSource.clear();
  }

  removeSelected(): void {
    const selected = this.getSelectedFeatures();
    selected.forEach(f => this.vectorSource.removeFeature(f));
    this.clearSelection();
  }

  getAllFeatures(): Feature<Geometry>[] {
    return this.vectorSource.getFeatures();
  }

  addFeature(feature: Feature<Geometry>): void {
    this.vectorSource.addFeature(feature);
  }

  removeFeature(feature: Feature<Geometry>): void {
    this.vectorSource.removeFeature(feature);
  }

  /**
   * Bestehende API beibehalten:
   * bei Klick wird ein Punkt-Feature erstellt und hinzugefügt.
   */
  waitForClickAndCreatePoint(): Promise<Feature<Point> | null> {
    return new Promise(resolve => {
      if (!this.map) {
        resolve(null);
        return;
      }

      this.map.once('singleclick', (evt: any) => {
        const coord = evt.coordinate;
        const feature = new Feature<Point>(new Point(coord));
        this.vectorSource.addFeature(feature);
        resolve(feature);
      });
    });
  }

  /**
   * Bestehende API beibehalten:
   * bei Klick wird ein Punkt-Feature erstellt und hinzugefügt.
   */
  waitForClickAndCreatePolygon(): Promise<Feature<Polygon> | null> {
    return new Promise(resolve => {
      if (!this.map) {
        resolve(null);
        return;
      }

      this.map.once('singleclick', (evt: any) => {
        const coord = evt.coordinate;
        const feature = new Feature<Polygon>(new Polygon(coord));
        this.vectorSource.addFeature(feature);
        resolve(feature);
      });
    });
  }

  // ---------------------------------------------------
  // Observable-Update
  // ---------------------------------------------------

  private updateFeatures(): void {
    this.featuresSubject.next(this.getAllFeatures());
  }

  // ---------------------------------------------------
  // Default-Style
  // ---------------------------------------------------

  private getDefaultStyle(): Style {
    return new Style({
      fill: new Fill({
        color: 'rgba(255, 255, 255, 0.2)',
      }),
      stroke: new Stroke({
        color: '#ffcc33',
        width: 2,
      }),
      image: new Circle({
        radius: 7,
        fill: new Fill({
          color: '#ffcc33',
        }),
      }),
    });
  }

  /**
   * Feature mit Metadata hinzufügen (z.B. campId, campName, camp-Objekt).
   * Beim Click kann man diese Properties auslesen.
   */
  public addFeatureWithMetadata(
    feature: Feature<Geometry>,
    metadata?: Record<string, any>
  ): void {
    if (metadata) {
      feature.setProperties(metadata);
    }
    this.vectorSource.addFeature(feature);
  }
}
