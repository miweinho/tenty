import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Location {
  x: number;
  y: number;
}

export interface Camp {
  id?: string;
  name: string;
  location: Location;
  delimitation?: Location[];
}

@Injectable({ providedIn: 'root' })
export class CampService {
  constructor(private http: HttpClient) {}

  // Send the payload in the shape the backend expects (no id on create)
  createCamp(camp: Omit<Camp, 'id'>): Observable<Camp> {
    return this.http.post<Camp>('/api/camp', camp);
  }

  // Fetch all camps
  getAll(): Observable<Camp[]> {
    return this.http.get<Camp[]>('/api/camp');
  }
}
