import type { Coordinate } from 'ol/coordinate';
import Point from 'ol/geom/Point';
import Polygon from 'ol/geom/Polygon';

export function degToRad(deg: number): number {
  return (deg * Math.PI) / 180;
}

export function radToDeg(rad: number): number {
  return (rad * 180) / Math.PI;
}

export function rectPolygonFromCenter3857(
  center: Coordinate,
  lengthM: number,
  widthM: number,
  headingDeg: number
): Polygon {
  const [x, y] = center;
  const hl = (Number(lengthM) || 0) / 2;
  const hw = (Number(widthM) || 0) / 2;

  const rot = degToRad(Number(headingDeg) || 0);
  const cos = Math.cos(rot);
  const sin = Math.sin(rot);

  const base: Array<[number, number]> = [
    [-hl, -hw],
    [ hl, -hw],
    [ hl,  hw],
    [-hl,  hw],
    [-hl, -hw],
  ];

  const coords = base.map(([dx, dy]) => {
    const rx = dx * cos - dy * sin;
    const ry = dx * sin + dy * cos;
    return [x + rx, y + ry] as Coordinate;
  });

  return new Polygon([coords]);
}

export function rotateHandlePointFromCenter3857(
  center: Coordinate,
  lengthM: number,
  headingDeg: number,
  offsetM = 20
): Point {
  const [x, y] = center;
  const rot = degToRad(Number(headingDeg) || 0);
  const dist = (Number(lengthM) || 0) / 2 + offsetM;

  const hx = x + Math.cos(rot) * dist;
  const hy = y + Math.sin(rot) * dist;

  return new Point([hx, hy]);
}