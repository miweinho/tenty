// src/app/components/camp-map/camp-overview-map-view.component.ts
import { Component, inject } from '@angular/core';
import Map from 'ol/Map';
import { MapComponent } from '../../pages/map/map';
import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { CampService } from '../../../service/camp';
import { CampDto, FullCampDto } from '../../../models';

@Component({
  selector: 'app-camp-overview-map-view',
  imports: [MapComponent],
  templateUrl: './camp-overview-map-view.html',
  styleUrls: ['./camp-overview-map-view.css'],
})
export class CampOverviewMapViewComponent extends BaseCampMapView {
  private campService = inject(CampService);

  protected override afterMapInitialized(_map: Map): void {
    this.loadCampsForOverview();
  }

private loadCampsForOverview(): void {
  this.campService.getCampsForUser().subscribe({
    next: (camps: CampDto[]) => {
      camps.forEach((camp) => {
        const locFeature = this.campMapHelper.featureFromDto(
          camp.location,
          { id: camp.id, name: camp.campName }
        );
        this.campMapHelper.applyCampLabelStyle(locFeature, camp.campName);
        this.drawingService.addFeature(locFeature);
      });

      this.fitViewToAllFeatures(); // nutzt jetzt die neue Heuristik
    },
    error: (err) => {
      console.error('Failed to load camps for overview', err);
    },
  });
}


}
