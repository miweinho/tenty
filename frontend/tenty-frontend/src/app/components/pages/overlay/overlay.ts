import { Component, inject, ViewChild } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule, MatDrawer } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { Router, RouterOutlet } from '@angular/router';
import { Auth } from '../../auth/auth';
import { CampService } from '../../../service/camps/camp';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

@Component({
  selector: 'app-overlay',
  standalone: true,
  imports: [
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
    MatListModule,
    RouterOutlet,
    CommonModule
  ],
  templateUrl: './overlay.html',
  styleUrl: './overlay.css',
})
export class Overlay {
  auth = inject(Auth);
  router = inject(Router);
  campService = inject(CampService);
  
  @ViewChild('drawer') drawer!: MatDrawer;

  hasCamp$: Observable<boolean>;

  constructor() {
    this.hasCamp$ = this.campService.camps$.pipe(
      map(camps => camps !== null && camps.length > 0)
    );
  }

  handleLogout() {
    this.auth.logout();
  }

  toggleDrawer() {
    this.drawer.toggle();
  }

  navigate(route: string) {
    this.router.navigate([route]);
    this.drawer.close();
  }
}
