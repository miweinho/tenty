import { inject, Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of, tap } from 'rxjs';
import { map } from 'rxjs/operators';
import { CampDto, CreateCampDto } from '../models';
import { HttpService } from '../components/http/http-service';


@Injectable({ providedIn: 'root' })
export class CampService {
  private http = inject(HttpService);

  private campListSubject = new BehaviorSubject<CampDto[] | null>(null);
  public readonly camps$ = this.campListSubject.asObservable();

  loadCampList(force = false): Observable<CampDto[]> {
    const cached = this.campListSubject.value;
    if (cached && !force) {
      return of(cached);
    }

    return this.http.get<any>('camp/list').pipe(
      // normalize backend response (handles { $values: [...] } wrapping)
      map(response => {
        const campsArray: CampDto[] = Array.isArray(response)
          ? response
          : response?.$values ?? response?.items ?? response?.data ?? [];
        return campsArray;
      }),
      tap(camps => this.campListSubject.next(camps))
    );
  }

  refreshCampsList(): Observable<CampDto[]> {
    return this.loadCampList(true);
  }

  getCurrentCamps(): CampDto[] | null {
    return this.campListSubject.value;
  }

  /**
   * Create a new camp
   */
  createCamp(campDto: CreateCampDto): Observable<CampDto> {
    return this.http.post<CampDto>('camp/create', campDto).pipe(
      tap((newCamp) => {
        console.log('Camp created successfully:', newCamp);
        // refresh camps list after successful creation
        this.refreshCampsList().subscribe();
      })
    );
  }

  /**
   * Update an existing camp
   */
  updateCamp(id: number, campDto: CreateCampDto): Observable<CampDto> {
    return this.http.put<CampDto>(`camp/update/${id}`, campDto).pipe(
      tap((updatedCamp) => {
        console.log('Camp updated successfully:', updatedCamp);
        // refresh camps list after successful update
        this.refreshCampsList().subscribe();
      })
    );
  }

  /**
   * Delete a camp
   */
  deleteCamp(id: number): Observable<void> {
    return this.http.delete<void>(`camp/delete/${id}`).pipe(
      tap(() => {
        console.log('Camp deleted successfully:', id);
        // refresh camps list after successful deletion
        this.refreshCampsList().subscribe();
      })
    );
  }
}
