import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Draw, Modify, Select, Snap } from 'ol/interaction';
import { Vector as VectorLayer } from 'ol/layer';
import { Vector as VectorSource } from 'ol/source';
import { Feature, Map } from 'ol';
import { Point, LineString, Polygon } from 'ol/geom';
import { Style, Fill, Stroke, Circle } from 'ol/style';

export type DrawType = 'Point' | 'LineString' | 'Polygon' | 'Circle';

@Injectable({
  providedIn: 'root'
})
export class DrawingService {
  private map?: Map;
  private vectorSource = new VectorSource();
  private vectorLayer = new VectorLayer({
    source: this.vectorSource,
    style: this.getDefaultStyle()
  });

  private drawInteraction?: Draw;
  private modifyInteraction?: Modify;
  private selectInteraction?: Select;

  // Observable für Features
  private featuresSubject = new BehaviorSubject<Feature[]>([]);
  public features$ = this.featuresSubject.asObservable();

  constructor() {
    // Listen to feature changes
    this.vectorSource.on('addfeature', () => this.updateFeatures());
    this.vectorSource.on('removefeature', () => this.updateFeatures());
  }

  initializeMap(map: Map): void {
    this.map = map;
    this.map.addLayer(this.vectorLayer);
    this.setupInteractions();
  }

  private setupInteractions(): void {
    if (!this.map) return;

    this.modifyInteraction = new Modify({ source: this.vectorSource });
    this.selectInteraction = new Select();

    this.map.addInteraction(this.modifyInteraction);
    this.map.addInteraction(this.selectInteraction);
  }

  startDrawing(type: DrawType): void {
    this.stopDrawing();
    
    if (!this.map) return;

    this.drawInteraction = new Draw({
      source: this.vectorSource,
      type: type
    });

    this.map.addInteraction(this.drawInteraction);

    // Add snap interaction
    const snapInteraction = new Snap({ source: this.vectorSource });
    this.map.addInteraction(snapInteraction);
  }

  stopDrawing(): void {
    if (this.drawInteraction && this.map) {
      this.map.removeInteraction(this.drawInteraction);
      this.drawInteraction = undefined;
    }
  }

  clearAll(): void {
    this.vectorSource.clear();
  }

  removeSelected(): void {
    if (this.selectInteraction) {
      const features = this.selectInteraction.getFeatures();
      features.forEach(feature => {
        this.vectorSource.removeFeature(feature);
      });
      features.clear();
    }
  }

  getAllFeatures(): Feature[] {
    return this.vectorSource.getFeatures();
  }

  addFeature(feature: Feature): void {
    this.vectorSource.addFeature(feature);
  }

  private updateFeatures(): void {
    this.featuresSubject.next(this.getAllFeatures());
  }

  /**
   * Wait for a single map click, create a point Feature at that click and add it to the vector source.
   * Resolves with the created Feature or null if map is not initialized.
   */
  waitForClickAndCreatePoint(): Promise<Feature | null> {
    return new Promise(resolve => {
      if (!this.map) { resolve(null); return; }
      this.map.once('singleclick', (evt: any) => {
        const coord = evt.coordinate;
        const feature = new Feature(new Point(coord));
        this.vectorSource.addFeature(feature);
        resolve(feature);
      });
    });
  }

  /**
   * Remove a feature from the vector source (useful when user cancels or backend save fails).
   */
  removeFeature(feature: Feature): void {
    this.vectorSource.removeFeature(feature);
  }

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
}