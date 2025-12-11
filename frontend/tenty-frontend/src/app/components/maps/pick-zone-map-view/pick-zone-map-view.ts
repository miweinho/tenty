import { Component, EventEmitter, Input, Output, ChangeDetectorRef, NgZone, inject, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import Map from 'ol/Map';
import Feature from 'ol/Feature';
import Polygon from 'ol/geom/Polygon';
import Point from 'ol/geom/Point';

import { MapComponent } from '../../pages/map/map';
import { BaseCampMapView } from '../../pages/mapview/base-camp-map-view';
import { GeoJsonFeatureDto, CampZoneDto } from '../../../models';
import { DrawingService } from '../../../service/drawingService/drawing-service';

import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-pick-zone-map-view',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MapComponent,
    MatButtonModule,
    MatToolbarModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule
  ],
  providers: [DrawingService],
  templateUrl: './pick-zone-map-view.html',
  styleUrls: ['./pick-zone-map-view.css'],
})
export class PickZoneMapView extends BaseCampMapView implements OnChanges {
  private cdr = inject(ChangeDetectorRef);
  private ngZone = inject(NgZone);
  private fb = inject(FormBuilder);

  /** Optional: existierende Camp-Zone zum Anzeigen/Bearbeiten */
  @Input() initialZone: Partial<CampZoneDto> | null = null;
  /** Optional: Camp-Location (Punkt) als Referenz auf der Karte */
  @Input() location: GeoJsonFeatureDto | null = null;

  @Output() zonePicked = new EventEmitter<CampZoneDto>();
  @Output() cancel = new EventEmitter<void>();

  private selectedFeature: Feature<Polygon> | null = null;
  private locationFeature: Feature<Point> | null = null;
  private isPicking = false;

  zoneForm: FormGroup = this.fb.group({
    name: ['', Validators.required],
    color: ['#ff0000']
  });

  ngOnChanges(changes: SimpleChanges): void {
    // Reagiere auf Location-Änderung, wenn Map bereits initialisiert
    if (changes['location'] && this.map) {
      this.updateLocationMarker();
    }
    
    if (changes['initialZone'] && this.initialZone) {
      this.zoneForm.patchValue({
        name: this.initialZone.name,
        color: this.initialZone.color
      });
    }
  }

  ngOnInit() {
    if (this.initialZone?.zone) {
      // verwende this.initialZone.zone
    }
    const name = this.initialZone?.name ?? 'Default';
    const color = this.initialZone?.color ?? '#ff0000';

    this.zoneForm.patchValue({
      name: name,
      color: color
    });
  }

  protected override afterMapInitialized(map: Map): void {
    console.log("init pick zone.");
    this.map = map;
    this.drawingService.clearAll();
    this.updateLocationMarker();

    // Vorhandene Zone anzeigen
    if (this.initialZone?.zone) {
      const feature = this.campMapHelper.featureFromDto(this.initialZone.zone, { kind: 'picked-zone' }) as Feature<Polygon>;
      this.drawingService.addFeature(feature);
      this.selectedFeature = feature;
      
      this.zoneForm.patchValue({
        name: this.initialZone.name,
        color: this.initialZone.color
      });
    }

    // View anpassen
    if (this.selectedFeature || this.locationFeature) {
      this.fitViewToAllFeatures();
    }
  }

  private updateLocationMarker(): void {
    // Alte Location entfernen
    if (this.locationFeature) {
      this.drawingService.removeFeature(this.locationFeature);
      this.locationFeature = null;
    }

    // Neue Location anzeigen
    if (this.location) {
      console.log("location vorhanden:", this.location);
      const loc = this.campMapHelper.featureFromDto(this.location, { kind: 'camp-location' }) as Feature<Point>;
      this.campMapHelper.applyCampLabelStyle(loc, 'Camp location');
      this.drawingService.addFeature(loc);
      this.locationFeature = loc;
      
      // View neu anpassen
      if (this.map) {
        this.fitViewToAllFeatures();
      }
    }
  }

  get hasSelection(): boolean {
    return !!this.selectedFeature;
  }

  get pickingActive(): boolean {
    return this.isPicking;
  }

  get isCampDelimitation(): boolean {
    return this.initialZone?.isCampDelimitation ?? true;
  }

  get showNameAndColor(): boolean {
    return !this.isCampDelimitation;
  }

  async startPickingZone(): Promise<void> {
    if (!this.map) return;

    this.ngZone.run(() => {
      this.isPicking = true;
      this.cdr.markForCheck();
    });

    if (this.selectedFeature) {
      this.drawingService.removeFeature(this.selectedFeature);
      this.selectedFeature = null;
    }

    // Einmaliges Polygon zeichnen
    const feature = await this.drawingService.drawOnce('Polygon');

    this.ngZone.run(() => {
      this.isPicking = false;
      if (feature) {
        this.selectedFeature = feature as Feature<Polygon>;
        this.fitViewToAllFeatures();
      }
      this.cdr.markForCheck();
    });
  }

  confirmZone(): void {
    if (!this.selectedFeature) return;

    // Validate name if not campDelimitation
    if (!this.isCampDelimitation && this.zoneForm.invalid) {
      this.zoneForm.markAllAsTouched();
      alert('Bitte Namen eingeben');
      return;
    }

    const zoneGeoJson = this.campMapHelper.dtoFromFeature(this.selectedFeature) as GeoJsonFeatureDto;

    const dto: CampZoneDto = {
      zone: zoneGeoJson,
      name: this.isCampDelimitation ? 'Camp Delimitation' : this.zoneForm.value.name,
      color: this.isCampDelimitation ? '#ff0000' : this.zoneForm.value.color,
      isCampDelimitation: this.isCampDelimitation
    };

    this.zonePicked.emit(dto);
  }

  clearZone(): void {
    if (this.selectedFeature) {
      this.drawingService.removeFeature(this.selectedFeature);
      this.selectedFeature = null;
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }
}
