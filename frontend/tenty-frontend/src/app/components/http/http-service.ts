import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class HttpService {
  private http = inject(HttpClient);
  private baseUrl = 'http://localhost:5038/api';

  private getHeaders(extraHeaders?: Record<string, string>): HttpHeaders {
    let headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    if (extraHeaders) {
      for (const [key, value] of Object.entries(extraHeaders)) {
        headers = headers.set(key, value);
      }
    }
    return headers;
  }

  get<T>(
    endpoint: string,
    params?: Record<string, any>,
    headers?: Record<string, string>
  ): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}/${endpoint}`, {
      headers: this.getHeaders(headers),
      params: new HttpParams({ fromObject: params || {} }),
    });
  }

  post<T>(endpoint: string, body: any, headers?: Record<string, string>): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}/${endpoint}`, body, {
      headers: this.getHeaders(headers),
    });
  }

  postText(endpoint: string, body: any, headers?: Record<string, string>): Observable<string> {
    return this.http.post(`${this.baseUrl}/${endpoint}`, body, {
      headers: this.getHeaders(headers),
      responseType: 'text',
    });
  }

  put<T>(endpoint: string, body: any, headers?: Record<string, string>): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}/${endpoint}`, body, {
      headers: this.getHeaders(headers),
    });
  }

  patch<T>(endpoint: string, body: any, headers?: Record<string, string>): Observable<T> {
    return this.http.patch<T>(`${this.baseUrl}/${endpoint}`, body, {
      headers: this.getHeaders(headers),
    });
  }

  delete<T>(endpoint: string, headers?: Record<string, string>): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}/${endpoint}`, {
      headers: this.getHeaders(headers),
    });
  }
}
