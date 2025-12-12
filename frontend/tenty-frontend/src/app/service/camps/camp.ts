import { inject, Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of, tap } from 'rxjs';
import { map } from 'rxjs/operators';
import { CampDto, CreateCampDto, FullCampDto, UpdateCampDto } from '../../models';
import { HttpService } from '../../components/http/http-service';


@Injectable({ providedIn: 'root' })
export class CampService {
  private http = inject(HttpService);

  private campListSubject = new BehaviorSubject<CampDto[] | null>(null);
  public readonly camps$ = this.campListSubject.asObservable();

  private campSubjects = new Map<number, BehaviorSubject<FullCampDto | null>>();

  loadCampList(force = false): Observable<CampDto[]> {
    const cached = this.campListSubject.value;
    if (cached && !force) {
      return of(cached);
    }

    return this.http.get<any>('Camp/list').pipe(
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
    return this.http.post<CampDto>('camp', campDto).pipe(
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
  updateCamp(id: number, campDto: UpdateCampDto): Observable<FullCampDto> {
    return this.http.put<FullCampDto>(`Camp/${id}`, campDto).pipe(
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
    return this.http.delete<void>(`Camp/${id}`).pipe(
      tap(() => {
        console.log('Camp deleted successfully:', id);
        // refresh camps list after successful deletion
        this.refreshCampsList().subscribe();
      })
    );
  }

  private ensureCampSubject(id: number): BehaviorSubject<FullCampDto | null> {
    if (!this.campSubjects.has(id)) {
      this.campSubjects.set(id, new BehaviorSubject<FullCampDto | null>(null));
    }
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    return this.campSubjects.get(id)!;
  }

  getCamp$(id: number): Observable<FullCampDto | null> {
    return this.ensureCampSubject(id).asObservable();
  }

  loadCamp(id: number, force = false): Observable<FullCampDto> {
    const subject = this.ensureCampSubject(id);
    const cached = subject.value;

    if (cached && !force) {
      return of(cached);
    }

    return this.http.get<FullCampDto>(`Camp/${id}`).pipe(tap((camp) => subject.next(camp)));
  }
}
