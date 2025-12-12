import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';
import { map } from 'rxjs';


export const routeGuard: CanActivateFn = (route, state) => {
  let authService = inject(Auth);
  const router = inject(Router);

  return authService.isLoggedIn$.pipe(
    map((loggedIn) => (loggedIn ? true: router.parseUrl('/login')))
  )
};

export const loginRedirctGuard: CanActivateFn = (route, state) => {
  let authService = inject(Auth);
  const router = inject(Router);

  return authService.isLoggedIn$.pipe(
    map((loggedIn) => (loggedIn ? router.parseUrl('/home') : true))
  )

}