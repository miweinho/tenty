import { Component, OnInit, inject, ViewChild } from '@angular/core';
import { DrawingService, DrawType } from '../../../service/drawingService/drawing-service';
import Map from 'ol/Map';
import { MapComponent } from '../map/map';
import Feature from 'ol/Feature';
import Point from 'ol/geom/Point';
import Circle from 'ol/style/Circle';
import { toLonLat } from 'ol/proj';
import { CampService, Camp } from '../../../service/camp';
import Style from 'ol/style/Style';
import Text from 'ol/style/Text';
import Fill from 'ol/style/Fill';
import Stroke from 'ol/style/Stroke';
import { CoordinateService } from '../../../service/coordinateService/coordinateService';

@Component({
  selector: 'app-mapview',
  imports: [MapComponent],
  templateUrl: './mapview.html',
  styleUrls: ['./mapview.css'],
})
export class MapView {
  @ViewChild(MapComponent, {static: true}) mapComponent!: MapComponent;
  drawingService = inject(DrawingService);
  coordinateService = inject(CoordinateService);
  campService = inject(CampService);

  onMapReady(map: Map): void {
    console.log("mapready from mapview?");
    this.drawingService.initializeMap(map);
    // load existing camps from backend and draw them
    //this.loadCamps();
  }

  startDrawing(type: DrawType): void {
    this.drawingService.startDrawing(type);
  }

  stopDrawing(): void {
    this.drawingService.stopDrawing();
  }

  clearAll(): void {
    this.drawingService.clearAll();
  }

  async addCamp(): Promise<void> {
    const feature: Feature | null = await this.drawingService.waitForClickAndCreatePoint();
    if (!feature) return; // map not initialized or cancelled

    const name = window.prompt('Enter camp name');
    if (!name) {
      // user cancelled -> remove the temporary feature
      this.drawingService.removeFeature(feature);
      return;
    }

    const geom: any = feature.getGeometry();
    const coords = geom?.getCoordinates?.();
    if (!coords) {
      this.drawingService.removeFeature(feature);
      return;
    }

    // transform to lon/lat if map projection is EPSG:3857
    const [lon, lat] = this.coordinateService.webMercatorToGps(coords[0], coords[1]);
    const [x, y] = this.coordinateService.gpsToLv95(lon, lat);

    // build payload in backend shape
    const campPayload: Omit<Camp, 'id'> = {
      name,
      location: { x: Math.round(x), y: Math.round(y) },
      delimitation: [] // single point camp -> empty delimitation; adjust if needed
    };

    this.campService.createCamp(campPayload).subscribe({
      next: saved => {
        // attach backend id/name to feature
        feature.setProperties({ id: saved.id, name: saved.name });

        // set a label style so the name is visible next to the point
        const labelStyle = new Style({
          text: new Text({
            text: saved.name,
            offsetY: -18, // move label above the point
            font: 'bold 12px Arial',
            fill: new Fill({ color: '#000' }),
            stroke: new Stroke({ color: '#fff', width: 3 }),
          })
        });
        feature.setStyle(labelStyle);
      },
      error: () => {
        this.drawingService.removeFeature(feature);
        alert('Failed to save camp');
      }
    });
  }

  // load all camps from backend and draw them on the map
  private loadCamps(): void {
    this.campService.getAll().subscribe({
      next: (camps: Camp[]) => {
        camps.forEach(c => {
          // backend location is LV95 (x,y) -> LV95 -> GPS -> WebMercator for map
          const [lon, lat] = this.coordinateService.lv95ToGps(c.location.x, c.location.y);
          const [mx, my] = this.coordinateService.gpsToWebMercator(lon, lat);

          const feat = new Feature(new Point([mx, my]));
          feat.setProperties({ id: c.id, name: c.name });

          const style = new Style({
            image: new Circle({
              radius: 6,
              fill: new Fill({ color: '#2b7cff' }),
              stroke: new Stroke({ color: '#fff', width: 2 })
            }),
            text: new Text({
              text: c.name,
              offsetY: -18,
              font: 'bold 12px Arial',
              fill: new Fill({ color: '#000' }),
              stroke: new Stroke({ color: '#fff', width: 3 })
            })
          });
          feat.setStyle(style);

          // add to vector source via DrawingService
          this.drawingService.addFeature(feat);
        });
      },
      error: (err) => {
        console.error('Failed to load camps', err);
      }
    });
  }
}
