import { Injectable } from '@angular/core';
import Feature, { type FeatureLike } from 'ol/Feature';
import Geometry from 'ol/geom/Geometry';
import Point from 'ol/geom/Point';
import Polygon from 'ol/geom/Polygon';
import Style, { type StyleFunction } from 'ol/style/Style';
import Text from 'ol/style/Text';
import Fill from 'ol/style/Fill';
import Stroke from 'ol/style/Stroke';
import GeoJSON from 'ol/format/GeoJSON';

import {
  GeoJsonFeatureDto,
  FullCampDto,
  CampZoneDto,
} from '../../models';

import CircleStyle from 'ol/style/Circle';
import { Circle } from 'ol/style';
import { rectPolygonFromCenter3857, rotateHandlePointFromCenter3857 } from './rect-geometry';

@Injectable({
  providedIn: 'root'
})
export class CampMapHelperService {
  private geoJson = new GeoJSON();

  featureFromDto(dto: GeoJsonFeatureDto, extraProps?: Record<string, any>): Feature<Geometry> {
    const mergedProps = {
      ...(dto.properties ?? {}),
      ...(extraProps ?? {}),
    };

    const feature = this.geoJson.readFeature(
      {
        type: 'Feature',
        geometry: dto.geometry,
        properties: mergedProps,
      },
      {
        dataProjection: 'EPSG:4326',
        featureProjection: 'EPSG:3857',
      }
    ) as Feature<Geometry>;

    if (feature.get('kind') === 'tent') {
      if (feature.get('lengthM') == null) feature.set('lengthM', 40);
      if (feature.get('widthM') == null) feature.set('widthM', 20);
      if (feature.get('headingDeg') == null) feature.set('headingDeg', 0);
      if (feature.get('icon') == null) feature.set('icon', 'tent');

      // WICHTIG: kein feature.setStyle(...) hier
    }

    return feature;
  }

  /**
   * Punkt-Feature für den Camp-Standort inkl. Label-Style.
   */
  createLocationFeatureFromCamp(camp: FullCampDto): Feature<Point> {
    const feature = this.featureFromDto(camp.location, {
      id: camp.id,
      name: camp.name,
    }) as Feature<Point>;

    this.applyCampLabelStyle(feature, camp.name);
    return feature;
  }

  /**
   * Polygon-Features für alle Camp-Zonen.
   */
  createZoneFeaturesFromCamp(camp: FullCampDto): Feature<Polygon>[] {
  return (camp.campZones ?? [])
    .map((zone: CampZoneDto) => {
      if (!zone.zone) return undefined;

      const feature = this.featureFromDto(zone.zone, {
        campId: camp.id,
        zoneName: zone.name,
        isCampDelimitation: zone.isCampDelimitation,
        color: zone.color,
      }) as Feature<Polygon>;

      feature.setStyle(this.createCampZoneStyle(zone.color));
      return feature;
    })
    .filter((feature): feature is Feature<Polygon> => feature !== undefined);
}

  // ---------------------------------------------------
  // OL Feature -> GeoJSON DTO
  // ---------------------------------------------------

  /**
   * Wandelt ein OL-Feature in ein GeoJsonFeatureDto um
   * (z.B. nach dem Zeichnen eines neuen Camps).
   */
  dtoFromFeature(feature: Feature<Geometry>): GeoJsonFeatureDto {
    const geom = feature.getGeometry();
    if (!geom) {
      throw new Error('Feature has no geometry');
    }

    const geometry = this.geoJson.writeGeometryObject(geom, {
      dataProjection: 'EPSG:4326',
      featureProjection: 'EPSG:3857',
    });

    const { geometry: _ignored, ...props } = feature.getProperties();

    return {
      type: 'Feature',
      geometry: geometry as any,
      properties: props,
    };
  }

  // Convenience für Punkte/Polygone, falls du strenger typisieren willst:
  dtoFromPointFeature(feature: Feature<Point>): GeoJsonFeatureDto {
    return this.dtoFromFeature(feature);
  }

  dtoFromPolygonFeature(feature: Feature<Polygon>): GeoJsonFeatureDto {
    return this.dtoFromFeature(feature);
  }

  // ---------------------------------------------------
  // Styling
  // ---------------------------------------------------

  applyCampLabelStyle(feature: Feature, name: string): void {
    feature.setStyle(this.createCampPointStyle(name));
    feature.set('name', name);
  }

  private createCampPointStyle(name: string): Style {
    return new Style({
      image: new Circle({
        radius: 6,
        fill: new Fill({ color: '#2b7cff' }),
        stroke: new Stroke({ color: '#fff', width: 2 }),
      }),
      text: new Text({
        text: name,
        offsetY: -18,
        font: 'bold 12px Arial',
        fill: new Fill({ color: '#000' }),
        stroke: new Stroke({ color: '#fff', width: 3 }),
      }),
    });
  }

  private createCampZoneStyle(color: string): Style {
    return new Style({
      stroke: new Stroke({
        color: color || '#ff6600',
        width: 2,
      }),
      fill: new Fill({
        color: 'rgba(255, 165, 0, 0.2)',
      }),
    });
  }

  private tentStyle(): StyleFunction {
    const rectStroke = new Stroke({ color: 'rgba(0, 90, 255, 0.95)', width: 2 });
    const rectFill = new Fill({ color: 'rgba(0, 90, 255, 0.15)' });

    const anchorStyle = new Style({
      image: new CircleStyle({
        radius: 5,
        fill: new Fill({ color: 'rgba(0,0,0,0.9)' }),
        stroke: new Stroke({ color: 'white', width: 2 }),
      }),
    });

    const handleStyle = new Style({
      image: new CircleStyle({
        radius: 6,
        fill: new Fill({ color: 'rgba(255,140,0,0.9)' }),
        stroke: new Stroke({ color: 'white', width: 2 }),
      }),
    });

    const rectStyle = new Style({
      fill: rectFill,
      stroke: rectStroke,
    });

    return (featureLike: FeatureLike, _resolution: number) => {
      // FeatureLike kann auch RenderFeature sein (Typing). Für unsere VectorLayer ist es i.d.R. ein echtes Feature.
      const feature = featureLike as Feature<Geometry>;
      const geom = (feature as any).getGeometry?.() as Geometry | undefined;
      if (!(geom instanceof Point)) return [];

      const center = geom.getCoordinates();
      const lengthM = Number((feature as any).get?.('lengthM') ?? 40);
      const widthM = Number((feature as any).get?.('widthM') ?? 20);
      const headingDeg = Number((feature as any).get?.('headingDeg') ?? 0);

      rectStyle.setGeometry(() => rectPolygonFromCenter3857(center, lengthM, widthM, headingDeg));
      handleStyle.setGeometry(() => rotateHandlePointFromCenter3857(center, lengthM, headingDeg, 20));

      return [rectStyle, handleStyle, anchorStyle];
    };
  }
}

