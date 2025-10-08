import { Injectable } from '@angular/core';
import proj4 from 'proj4';
import { register } from 'ol/proj/proj4';

@Injectable({
  providedIn: 'root'
})
export class CoordinateService {
  
  constructor() {
    this.initializeProjections();
  }

  private initializeProjections(): void {
    // Schweizer LV95 Projektion (EPSG:2056)
    proj4.defs('EPSG:2056', '+proj=somerc +lat_0=46.95240555555556 +lon_0=7.439583333333333 +k_0=1 +x_0=2600000 +y_0=1200000 +ellps=bessel +towgs84=674.374,15.056,405.346,0,0,0,0 +units=m +no_defs');
    
    // GPS Koordinaten (WGS84)
    proj4.defs('EPSG:4326', '+proj=longlat +datum=WGS84 +no_defs');
    
    register(proj4);
  }

  // Schweizer LV95 zu GPS (Länge/Breite)
  lv95ToGps(x: number, y: number): [number, number] {
    return proj4('EPSG:2056', 'EPSG:4326', [x, y]);
  }

  // GPS zu Schweizer LV95
  gpsToLv95(longitude: number, latitude: number): [number, number] {
    return proj4('EPSG:4326', 'EPSG:2056', [longitude, latitude]);
  }

  // Web Mercator zu GPS
  webMercatorToGps(x: number, y: number): [number, number] {
    return proj4('EPSG:3857', 'EPSG:4326', [x, y]);
  }

  // GPS zu Web Mercator
  gpsToWebMercator(longitude: number, latitude: number): [number, number] {
    return proj4('EPSG:4326', 'EPSG:3857', [longitude, latitude]);
  }
}