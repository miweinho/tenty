import { HttpRequest, HttpHandlerFn, HttpErrorResponse } from "@angular/common/http";
import { inject } from '@angular/core';
import { Auth } from "./auth";
import { catchError, switchMap } from 'rxjs/operators';
import { throwError } from 'rxjs';

export function authInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  const auth = inject(Auth);

  const token = auth.getAccessToken();
  const authReq = token
    ? req.clone({ headers: req.headers.append('Authorization', `Bearer ${token}`) })
    : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        // Try refresh once, then retry request; logout if refresh fails
        return auth.refresh().pipe(
          switchMap(() => {
            const refreshedToken = auth.getAccessToken();
            const retryReq = refreshedToken
              ? authReq.clone({ headers: authReq.headers.set('Authorization', `Bearer ${refreshedToken}`) })
              : authReq;
            return next(retryReq);
          }),
          catchError(() => {
            auth.logout();
            return throwError(() => err);
          })
        );
      }
      return throwError(() => err);
    })
  );
}
