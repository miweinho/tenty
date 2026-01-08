import { Component, EventEmitter, Output, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-map-toolbar',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule],
  templateUrl: './map-toolbar.html',
  styleUrls: ['./map-toolbar.css'],
})
export class MapToolbarComponent {
  @Input() dirty = false;
  @Output() drawLine = new EventEmitter<void>();
  @Output() drawObject = new EventEmitter<void>();
  @Output() undo = new EventEmitter<void>();
  @Output() redo = new EventEmitter<void>();
  @Output() refresh = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() openCampInfo = new EventEmitter<void>();

}
