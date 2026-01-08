import { Component, AfterViewInit, Output, EventEmitter, inject, ViewChild, ElementRef } from '@angular/core';
import 'ol/ol.css';
import Map from 'ol/Map';
import View from 'ol/View';
import { OSM, XYZ } from 'ol/source';
import TileLayer from 'ol/layer/Tile';
import {register} from 'ol/proj/proj4'
import proj4 from 'proj4'
import { CoordinateService } from '../../../service/coordinateService/coordinateService';

@Component({
  selector: 'app-map',
  templateUrl: './map.html',
  styleUrls: ['./map.css'],
})
export class MapComponent implements AfterViewInit {
  public map!: Map;
  private coordinateService = inject(CoordinateService);

  @Output() mapReady = new EventEmitter<Map>();
  @ViewChild('mapContainer', { static: false }) mapContainer!: ElementRef<HTMLDivElement>;


  ngAfterViewInit(): void {
    const [webMercatorX, webMercatorY] = this.coordinateService.gpsToWebMercator(8.2266, 46.8011);

    this.map = new Map({
      target: this.mapContainer.nativeElement,
      layers: [
        new TileLayer({
          source: new XYZ({
            url: `https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{y}.jpeg`
          })
        }),
      ],
      view: new View({
        projection: "EPSG:3857",
        center: [webMercatorX, webMercatorY],
        zoom: 8,
        minZoom: 6,
        maxZoom: 19,
      }),
    });
    this.mapReady.emit(this.map);
  }

    // public API: force OL to recalculate size when container becomes visible
  public updateSize(): void {
    try {
      if (this.map && typeof (this.map as any).updateSize === 'function') {
        (this.map as any).updateSize();
      }
    } catch (e) {
      console.warn('updateSize failed', e);
    }
  }

  getMap(): Map {
    return this.map;
  }
}

