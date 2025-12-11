import { Component, inject, OnInit } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { CampOverviewMapViewComponent } from '../../maps/camp-overview-map-view/camp-overview-map-view';
import { CommonModule } from '@angular/common';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { CampService } from '../../../service/camps/camp';
import { Router } from '@angular/router';
import { CampDto } from '../../../models';

@Component({
  selector: 'app-home',
  imports: [MatTabsModule, MatCardModule, CampOverviewMapViewComponent, CommonModule, MatTabsModule, MatCardModule, MatListModule, MatIconModule, MatButtonModule, MatDividerModule, CampOverviewMapViewComponent],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home implements OnInit {
  private campService = inject(CampService);
  private router = inject(Router);

  public camps$ = this.campService.camps$;

  ngOnInit(): void {
      this.campService.loadCampList().subscribe();
  }

    onCampSelect(camp: CampDto): void {
    console.log('Camp selected:', camp);
    // TODO: navigate to camp detail page or emit event
    // this.router.navigate(['/camps', camp.id]);
  }

  onMapCampSelected(camp: CampDto): void {
    console.log("onmapCampselected: ");
    this.onCampSelect(camp);
  }
}
