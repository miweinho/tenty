import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap, map, catchError } from 'rxjs/operators';
import { LoginDTO, TokenPair } from '../../models';

import { HttpService } from '../http/http-service';
import { Router } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class Auth {
  constructor() {
    this.initialize(); // Service-Start
  }

  private router = inject(Router);
  private http = inject(HttpService);
  private ACCESS_KEY = 'tenty_access_token';
  private REFRESH_KEY = 'tenty_refresh_token';
  private _isLoggedIn$ = new BehaviorSubject<boolean>(this.hasValidAccessToken());
  public readonly isLoggedIn$ = this._isLoggedIn$.asObservable();

  private initialize(): void {
    console.log("hello from initailize auth");
    // 1) Wenn Access noch gültig -> loggedIn = true
    if (this.hasValidAccessToken()) {
      console.log("has valid acces token!");
      this._isLoggedIn$.next(true);
      return;
    }

    // 2) Wenn Refresh-Token vorhanden -> versuche silent refresh
    const refresh = this.getRefreshToken();
    if (refresh) {
      this.refresh().subscribe({
        next: () => {
          // refresh hat neue Tokens gespeichert
          this._isLoggedIn$.next(true);
        },
        error: () => {
          // refresh fehlgeschlagen -> Tokens löschen / ausloggen
          this.clearToken();
          this._isLoggedIn$.next(false);
        }
      });
      return;
    }

    // 3) kein valider Token und kein Refresh -> ausgeloggt
    this.clearToken();
    this._isLoggedIn$.next(false);
  }

  login(dto: LoginDTO): Observable<void> {
    return this.http.post<TokenPair>('User/login', dto).pipe(
      tap(pair => {
        this.saveToken(pair); 
        this.router.navigateByUrl('/');
        }),
      map(() => void 0),
      catchError(err => {
        // do not change loggedInSubject here; caller handles errors
        throw err;
      })
    );
  }

    refresh(): Observable<void> {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      return this.http.post<TokenPair>('User/refresh', { token: refreshToken }).pipe(
        tap(pair => this.saveToken(pair)),
        map(() => void 0),
        catchError(err => { throw err; })
      );
    }
    return new Observable<void>((subscriber) =>{
      subscriber.error(new Error('no refresh token available'));
      this.router.navigateByUrl('/login');
    })
  }

  logout(): void {
    this.clearToken();
    this._isLoggedIn$.next(false);
    this.router.navigateByUrl('/login');
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.ACCESS_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_KEY);
  }

  private saveToken(pair : TokenPair) {
    if (pair.accessToken) localStorage.setItem(this.ACCESS_KEY, pair.accessToken);
    if (pair.refreshToken) localStorage.setItem(this.REFRESH_KEY, pair.refreshToken);
    if (pair.accessExpiresAt) localStorage.setItem(`${this.ACCESS_KEY}_exp`, pair.accessExpiresAt);
    if (pair.refreshExpiresAt) localStorage.setItem(`${this.REFRESH_KEY}_exp`, pair.refreshExpiresAt);
    this._isLoggedIn$.next(true);

  }

  private clearToken(): void {
    localStorage.removeItem(this.ACCESS_KEY);
    localStorage.removeItem(this.REFRESH_KEY);
    localStorage.removeItem(`${this.ACCESS_KEY}_exp`);
    localStorage.removeItem(`${this.REFRESH_KEY}_exp`);
    this._isLoggedIn$.next(false);
  }

   hasValidAccessToken(clockSkewSeconds = 60): boolean {
    const nowMs = Date.now();
    const skewMs = clockSkewSeconds * 1000;

    const token = this.getAccessToken();
    if (!token) return false;

    const expVal =
      localStorage.getItem(`${this.ACCESS_KEY}_exp`) ??
      sessionStorage.getItem(`${this.ACCESS_KEY}_exp`);
    if (!expVal) return false;

    const parsed = Date.parse(expVal);
    if (!isNaN(parsed)) return parsed > nowMs + skewMs;

    const num = Number(expVal);
    if (!Number.isNaN(num) && isFinite(num)) {
      const millis = num < 1e12 ? num * 1000 : num;
      return millis > nowMs + skewMs;
    }
    return false;
  }
}
