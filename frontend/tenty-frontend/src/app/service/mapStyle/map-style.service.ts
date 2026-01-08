import { Injectable } from '@angular/core';
import type { FeatureLike } from 'ol/Feature';
import Feature from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';
import Point from 'ol/geom/Point';
import { Style, Fill, Stroke, Circle, Text } from 'ol/style';
import CircleStyle from 'ol/style/Circle';
import type { StyleFunction } from 'ol/style/Style';

import { rectPolygonFromCenter3857, rotateHandlePointFromCenter3857 } from '../campMapHelperService/rect-geometry';

@Injectable({ providedIn: 'root' })
export class MapStyleService {
  // Cache nur für “Basisstyles” ohne Text (Text hängt oft vom Feature ab)
  private readonly tentBaseCache = new Map<string, Style[]>();

  /** StyleFunction für OL Layer: niemals feature.setStyle im Normalfall nötig. */
  public readonly layerStyleFn: StyleFunction = (featureLike: FeatureLike) => {
    return this.getStyleForFeature(featureLike);
  };

  private getStyleForFeature(feature: FeatureLike): Style | Style[] {
    const kind = feature.get('kind');
    const name = String(feature.get('name') ?? '');

    // Hex-only (immer "#RRGGBB")
    const color = this.normalizeHex(String(feature.get('color') ?? ''), '#ffcc33');

    switch (kind) {
      case 'tent':
        return this.getTentStyles(feature, color, name);
      case 'camp-location':
        return this.getCampLocationStyle(color, name);
      case 'camp-delimitation':
        return this.getDelimitationStyle(color, name);
      case 'camp-zone':
        return this.getCampZoneStyle(color, name);
      default:
        return this.getDefaultStyle(color, name);
    }
  }

  // -------------------------
  // Tent (Point -> Rechteck via Style-Geometry)
  // -------------------------

  private getTentStyles(featureLike: FeatureLike, color: string, name: string): Style[] {
    const lengthM = Number(featureLike.get('lengthM') ?? 40);
    const widthM = Number(featureLike.get('widthM') ?? 20);

    const labelText = name
      ? `${name}\n${lengthM}m × ${widthM}m`
      : `${lengthM}m × ${widthM}m`;

    const label = new Style({
      text: new Text({
        text: labelText,
        font: 'bold 12px Arial, sans-serif',
        fill: new Fill({ color: '#000000' }),
        stroke: new Stroke({ color: '#ffffff', width: 3 }),
        offsetY: -20,
      }),
      zIndex: 40,
    });

    return [...this.getTentBaseStyles(color), label];
  }

  private getTentBaseStyles(color: string): Style[] {
    const key = color;
    const cached = this.tentBaseCache.get(key);
    if (cached) return cached;

    const rect = new Style({
      // Hex-only => keine Transparenz
      fill: new Fill({ color }),
      stroke: new Stroke({ color, width: 3 }),
      zIndex: 10,
    });

    rect.setGeometry((featureLike: FeatureLike) => {
      const f = featureLike as Feature<Geometry>;
      const geom = f.getGeometry?.();
      if (!(geom instanceof Point)) return geom as any;

      const center = geom.getCoordinates();
      const lengthM = Number(f.get('lengthM') ?? 40);
      const widthM = Number(f.get('widthM') ?? 20);
      const headingDeg = Number(f.get('headingDeg') ?? 0);

      return rectPolygonFromCenter3857(center, lengthM, widthM, headingDeg) as any;
    });

    const handle = new Style({
      image: new CircleStyle({
        radius: 6,
        fill: new Fill({ color: 'rgba(255,140,0,0.9)' }),
        stroke: new Stroke({ color: '#ffffff', width: 2 }),
      }),
      zIndex: 20,
    });

    handle.setGeometry((featureLike: FeatureLike) => {
      const f = featureLike as Feature<Geometry>;
      const geom = f.getGeometry?.();
      if (!(geom instanceof Point)) return geom as any;

      const center = geom.getCoordinates();
      const lengthM = Number(f.get('lengthM') ?? 40);
      const headingDeg = Number(f.get('headingDeg') ?? 0);

      return rotateHandlePointFromCenter3857(center, lengthM, headingDeg, 20) as any;
    });

    const anchor = new Style({
      image: new CircleStyle({
        radius: 5,
        fill: new Fill({ color: 'rgba(0,0,0,0.9)' }),
        stroke: new Stroke({ color: '#ffffff', width: 2 }),
      }),
      zIndex: 30,
    });

    const styles = [rect, handle, anchor];
    this.tentBaseCache.set(key, styles);
    return styles;
  }

  // -------------------------
  // Other kinds
  // -------------------------

  private getCampLocationStyle(color: string, name: string): Style {
    return new Style({
      image: new Circle({
        radius: 10,
        fill: new Fill({ color }),
        stroke: new Stroke({ color: '#ffffff', width: 2 }),
      }),
      text: new Text({
        text: name,
        font: 'bold 14px Arial, sans-serif',
        fill: new Fill({ color: '#000000' }),
        stroke: new Stroke({ color: '#ffffff', width: 3 }),
        offsetY: -20,
      }),
    });
  }

  private getDelimitationStyle(color: string, name: string): Style {
    return new Style({
      // Hex-only => keine Transparenz
      fill: new Fill({ color }),
      stroke: new Stroke({ color, width: 2, lineDash: [10, 5] }),
      text: new Text({
        text: name,
        font: '12px Arial, sans-serif',
        fill: new Fill({ color: '#000000' }),
        stroke: new Stroke({ color: '#ffffff', width: 3 }),
        offsetY: -15,
      }),
    });
  }

  private getCampZoneStyle(color: string, name: string): Style {
    return new Style({
      stroke: new Stroke({ color, width: 2 }),
      // Hex-only => keine Transparenz
      fill: new Fill({ color }),
      text: new Text({
        text: name ?? '',
        font: '12px Arial, sans-serif',
        fill: new Fill({ color: '#000000' }),
        stroke: new Stroke({ color: '#ffffff', width: 3 }),
      }),
    });
  }

  private getDefaultStyle(color: string, name: string): Style {
    return new Style({
      // Hex-only => keine Transparenz
      fill: new Fill({ color }),
      stroke: new Stroke({ color, width: 2 }),
      image: new Circle({ radius: 7, fill: new Fill({ color }) }),
      text: new Text({
        text: name,
        font: '12px Arial, sans-serif',
        fill: new Fill({ color: '#000000' }),
        stroke: new Stroke({ color: '#ffffff', width: 3 }),
        offsetY: -15,
      }),
    });
  }

  // -------------------------
  // Hex-only helpers
  // -------------------------

  /**
   * Akzeptiert nur "#RRGGBB" oder "RRGGBB".
   * Alles andere -> fallback.
   */
  private normalizeHex(input: string, fallback: string): string {
    const raw = (input ?? '').trim();
    const fb = this.isHex6(fallback) ? this.toHashHex6(fallback) : '#ffcc33';

    if (!raw) return fb;
    if (this.isHex6(raw)) return this.toHashHex6(raw);
    return fb;
  }

  private isHex6(s: string): boolean {
    return /^#?[0-9a-fA-F]{6}$/.test((s ?? '').trim());
  }

  private toHashHex6(s: string): string {
    const v = s.trim();
    return v.startsWith('#') ? v : `#${v}`;
  }
}