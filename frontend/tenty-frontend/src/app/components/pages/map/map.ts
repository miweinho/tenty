import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
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
export class MapComponent implements OnInit {
  public map!: Map;
  private coordinateService = inject(CoordinateService);

  @Output() mapReady = new EventEmitter<Map>();


  ngOnInit(): void {
    const [webMercatorX, webMercatorY] = this.coordinateService.gpsToWebMercator(8.898, 47.5558);

    this.map = new Map({
      target: 'map',
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
        zoom: 18
      }),
    });
    this.mapReady.emit(this.map);
  }

  getMap(): Map {
    return this.map;
  }
}

