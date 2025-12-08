import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MapView } from '../mapview/mapview';
import { CampOverviewMapViewComponent } from '../../maps/camp-overview-map-view/camp-overview-map-view';

@Component({
  selector: 'app-home',
  imports: [MatTabsModule, MatCardModule, MapView, CampOverviewMapViewComponent],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {

  private loadCampsForOverview() : void {
    
  }

}
