import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { FeatureDto, CreateFeatureDto } from '../../models';
import { HttpService } from '../../components/http/http-service';

@Injectable({ providedIn: 'root' })
export class FeatureService {
  private http = inject(HttpService);

  private featuresSubject = new BehaviorSubject<FeatureDto[] | null>(null);
  public readonly features$ = this.featuresSubject.asObservable();

  /**
   * Load features for a camp.
   */
  loadFeaturesForCamp(campId: number): Observable<FeatureDto[]> {
    return this.http
      .get<FeatureDto[]>(`Feature/camp/${campId}`)
      .pipe(tap((features) => this.featuresSubject.next(features)));
  }

  refreshFeatures(campId: number): Observable<FeatureDto[]> {
    return this.loadFeaturesForCamp(campId);
  }

  /**
   * Create a single feature and update local state.
   */
  createFeature(dto: CreateFeatureDto): Observable<FeatureDto> {
    return this.http.post<FeatureDto>('Feature', dto).pipe(
      tap((created) => {
        const current = this.featuresSubject.value ?? [];
        this.featuresSubject.next([...current, created]);
      })
    );
  }

  /**
   * Update an existing feature by id.
   */
  updateFeature(id: number, dto: FeatureDto): Observable<FeatureDto> {
    return this.http.put<FeatureDto>(`Feature/${id}`, dto);
  }

  /**
   * Delete a feature by id.
   */
  deleteFeature(id: number): Observable<void> {
    return this.http.delete<void>(`Feature/${id}`);
  }

  setFeatures(features: FeatureDto[]): void {
    this.featuresSubject.next(features);
  }
}
