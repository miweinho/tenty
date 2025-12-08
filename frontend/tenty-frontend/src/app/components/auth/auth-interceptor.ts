import { HttpRequest, HttpHandlerFn, HttpErrorResponse } from "@angular/common/http";
import { inject } from '@angular/core';
import { Auth } from "./auth";
import { catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';


export function authInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  const auth = inject(Auth);

  const authToken = auth.getAccessToken();
  const newReq = authToken ? req.clone({
    headers: req.headers.append('Authorization', `Bearer ${authToken}`),
  }) : req;

  return next(newReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err && (err.status === 401)) {
        auth.logout();
        
      } else {
        auth.refresh();
      }
      return throwError(() => err);
    })
  );
}
