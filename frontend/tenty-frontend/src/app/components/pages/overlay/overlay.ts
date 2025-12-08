import { Component, inject } from '@angular/core';
import {MatIconModule} from '@angular/material/icon';
import {MatButtonModule} from '@angular/material/button';
import {MatToolbarModule} from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';
import { Auth } from '../../auth/auth';

@Component({
  selector: 'app-overlay',
  imports: [MatToolbarModule, MatButtonModule, MatIconModule, RouterOutlet],
  templateUrl: './overlay.html',
  styleUrl: './overlay.css',
})
export class Overlay {
  auth = inject(Auth);

  handleLogout() {
    this.auth.logout();
  }

}
