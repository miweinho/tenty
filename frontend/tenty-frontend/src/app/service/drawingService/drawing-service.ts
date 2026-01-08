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
import DragPan from 'ol/interaction/DragPan';
import type MapBrowserEvent from 'ol/MapBrowserEvent';
import { rotateHandlePointFromCenter3857, radToDeg } from '../campMapHelperService/rect-geometry';
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

  private dragPan?: DragPan;

  private tentDragging = false;
  private tentRotating = false;
  private lastCoord: [number, number] | null = null;

  private viewportPointerDownHandler?: (e: PointerEvent) => void;
  private viewportPointerUpHandler?: (e: PointerEvent) => void;
  private tentEventsBoundForMap: Map | null = null;

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

  /**
   * Emits ONLY when an interaction is "committed":
   * - drawend
   * - modifyend
   * - pointerup after tent drag/rotate
   */
  private readonly featuresCommittedSubject = new Subject<Feature<Geometry>[]>();
  public readonly featuresCommitted$ = this.featuresCommittedSubject.asObservable();

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
      // Tent events von alter Map lösen
      this.unbindTentTransformEvents(this.map);

      this.map.removeLayer(this.vectorLayer);
      this.removeInteractions(this.map);
    }

    this.map = map;

    // DragPan suchen (damit wir es während Drag/Rotate deaktivieren können)
    this.dragPan = map
      .getInteractions()
      .getArray()
      .find((i) => i instanceof DragPan) as DragPan | undefined;

    this.map.addLayer(this.vectorLayer);
    this.setupInteractions();
    this.setupClickListener();

    this.bindTentTransformEvents(); // <-- jetzt sicher (typed)
  }

  private setupInteractions(): void {
    if (!this.map) return;

    this.modifyInteraction = new Modify({ source: this.vectorSource });
    this.map.addInteraction(this.modifyInteraction);

    // Commit after modify end (not on every move)
    this.modifyInteraction.on('modifyend', (evt: any) => {
      const arr = evt?.features?.getArray?.() as Feature<Geometry>[] | undefined;
      if (arr && arr.length) this.featuresCommittedSubject.next(arr);
    });

    // IMPORTANT: Select must not override your tent style (handle disappearing issue)
    this.selectInteraction = new Select({
      style: null,
      hitTolerance: 6,
      layers: [this.vectorLayer],
    });
    this.map.addInteraction(this.selectInteraction);

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
        draw.un('drawend', onDrawEnd);
        if (this.map) {
          this.map.removeInteraction(draw);
        }
        this.drawInteraction = null;

        // Commit after draw end
        this.featuresCommittedSubject.next([feature]);

        resolve(feature);
      };

      draw.on('drawend', onDrawEnd);

      this.map.addInteraction(draw);
      this.drawInteraction = draw;
    });
  }

  // In deiner Tent-Transform-Logik (pointerup) ergänzen:
  // (Falls du diese Logik schon drin hast: genau dort einfügen)
  private commitSelectedIfTentInteractionFinished(): void {
    const f = this.getSelectedFeature?.() as Feature<Geometry> | null;
    if (!f) return;
    this.featuresCommittedSubject.next([f]);
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

  private setPanning(enabled: boolean) {
    this.dragPan?.setActive(enabled);
  }

  private getSelectedFeature(): Feature<Geometry> | null {
    if (!this.selectInteraction) return null;
    return this.selectInteraction.getFeatures().item(0) ?? null;
  }

  private unbindTentTransformEvents(map: Map) {
    if (this.tentEventsBoundForMap !== map) return;

    const viewport = map.getViewport();
    if (this.viewportPointerDownHandler) {
      viewport.removeEventListener('pointerdown', this.viewportPointerDownHandler);
      this.viewportPointerDownHandler = undefined;
    }
    if (this.viewportPointerUpHandler) {
      viewport.removeEventListener('pointerup', this.viewportPointerUpHandler);
      this.viewportPointerUpHandler = undefined;
    }

    this.tentEventsBoundForMap = null;
  }

  private bindTentTransformEvents() {
    if (!this.map) return;
    if (this.tentEventsBoundForMap === this.map) return; // schon gebunden

    // Falls noch auf einer alten Map gebunden (sollte selten sein), lösen
    if (this.tentEventsBoundForMap) {
      this.unbindTentTransformEvents(this.tentEventsBoundForMap);
    }

    const map = this.map;
    const viewport = map.getViewport();

    this.viewportPointerDownHandler = (e: PointerEvent) => {
      const f = this.getSelectedFeature();
      if (!f) return;
      if (f.get('kind') !== 'tent') return;

      const geom = f.getGeometry();
      if (!(geom instanceof Point)) return;

      const pixel = map.getEventPixel(e);
      const coord = map.getCoordinateFromPixel(pixel) as [number, number];
      if (!coord) return;

      const center = geom.getCoordinates() as [number, number];

      const lengthM = Number(f.get('lengthM') ?? 40);
      const headingDeg = Number(f.get('headingDeg') ?? 0);

      const handlePoint = rotateHandlePointFromCenter3857(center, lengthM, headingDeg, 20);
      const [hx, hy] = handlePoint.getCoordinates() as [number, number];

      const res = map.getView().getResolution() ?? 1;
      const tol = 10 * res; // ~10px in Karten-Einheiten
      const dx = coord[0] - hx;
      const dy = coord[1] - hy;

      this.tentRotating = (dx * dx + dy * dy) <= tol * tol;
      this.tentDragging = !this.tentRotating;

      this.lastCoord = coord;
      this.setPanning(false);
      e.preventDefault();
    };

    this.viewportPointerUpHandler = (e: PointerEvent) => {
      if (this.tentDragging || this.tentRotating) {
        e.preventDefault();
      }
      this.tentDragging = false;
      this.tentRotating = false;
      this.lastCoord = null;
      this.setPanning(true);
    };

    viewport.addEventListener('pointerdown', this.viewportPointerDownHandler);
    viewport.addEventListener('pointerup', this.viewportPointerUpHandler);

    map.on('pointermove', (evt) => {
      if (!this.tentDragging && !this.tentRotating) return;

      const f = this.getSelectedFeature();
      if (!f || f.get('kind') !== 'tent') return;

      const geom = f.getGeometry();
      if (!(geom instanceof Point)) return;

      const center = geom.getCoordinates() as [number, number];

      if (this.tentRotating) {
        const mx = evt.coordinate[0];
        const my = evt.coordinate[1];
        const rad = Math.atan2(my - center[1], mx - center[0]);
        f.set('headingDeg', radToDeg(rad));
        f.changed();
        evt.preventDefault();
        return;
      }

      if (this.tentDragging && this.lastCoord) {
        const [x0, y0] = this.lastCoord;
        const [x1, y1] = evt.coordinate as [number, number];

        geom.setCoordinates([center[0] + (x1 - x0), center[1] + (y1 - y0)]);
        f.changed();

        this.lastCoord = [x1, y1];
        evt.preventDefault();
      }
    });

    this.tentEventsBoundForMap = map;
  }

  /**
   * Setzt Center & Zoom basierend auf allen Features im DrawingService.
   * Kein view.fit, sondern eigene Heuristik:
   *  - extents zusammenfassen
   *  - Center = Mitte des Extents
   *  - Zoom abhängig von der Größe des Extents (in Metern, EPSG:3857)
   */
  fitViewToAllFeatures(): void {
    if (!this.map) return;
    const features: Feature<Geometry>[] = this.getAllFeatures();
    if (!features.length) return;

    const extent = createEmpty();
    features.forEach((f) => {
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
      zoom = 16; // ~1 km -> Stadtteil
    } else if (maxDim < 5000) {
      zoom = 14; // einige km
    } else if (maxDim < 20000) {
      zoom = 10; // Stadt / Region
    } else {
      zoom = 8; // weiter auseinander -> Region
    }

    view.setCenter(center);
    view.setZoom(zoom);
  }
}
