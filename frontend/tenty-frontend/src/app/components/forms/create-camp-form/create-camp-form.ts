import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  FormBuilder,
  FormArray,
  FormControl,
  FormGroup,
  Validators,
  ReactiveFormsModule,
  AbstractControl,
} from '@angular/forms';
import { GeoJsonFeatureDto, CampZoneDto, CampDetailDto, CreateCampDto } from '../../../models';
import { MatCardModule } from '@angular/material/card';
import { MatStepperModule } from '@angular/material/stepper';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule, MatLabel } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { CommonModule } from '@angular/common';
import { MatInputModule } from '@angular/material/input';
import { CampService } from '../../../service/camps/camp';
import { PickLocationMapViewComponent } from '../../maps/pick-location-map-view/pick-location-map-view';
import { PickZoneMapView } from '../../maps/pick-zone-map-view/pick-zone-map-view';

@Component({
  selector: 'app-create-camp-form',
  imports: [
    MatCardModule,
    MatStepperModule,
    MatIconModule,
    MatLabel,
    MatFormFieldModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatNativeDateModule,
    ReactiveFormsModule,
    CommonModule,
    MatInputModule,
    PickLocationMapViewComponent,
    PickZoneMapView
  ],
  templateUrl: './create-camp-form.html',
  styleUrls: ['./create-camp-form.css'],
})
export class CreateCampForm {
  private fb = inject(FormBuilder);
  private campService = inject(CampService);
  private router = inject(Router);

  isSubmitting = false; // Loading-State
  //
  mode = "campDelimitation";

  // form structure
  campForm = this.fb.group({
    // step 1: location (required)
    location: this.fb.control<GeoJsonFeatureDto | null>(null, { validators: Validators.required }),

    // step 2: delimitation
    // zone hat einen required-Validator als Platzhalter
    delimitation: this.fb.group({
      name: this.fb.control<string>('', { nonNullable: true }),
      isCampDelimitation: this.fb.control<boolean>(true, { nonNullable: true }),
      color: this.fb.control<string>('#ff0000', { nonNullable: true }),
      zone: this.fb.control<GeoJsonFeatureDto | null>(
        null,
        { validators: Validators.required } // TODO: bei Bedarf Validator-Regeln anpassen
      ),
    }),

    // step 3: rest
    name: this.fb.control<string>('', {
      nonNullable: true,
      validators: Validators.required,
    }),
    isSubCamp: this.fb.control<boolean>(false, { nonNullable: true }),
    parentId: this.fb.control<number | null>(null),

    // FormArray für Subcamp-IDs (nummerische IDs)
    subCamp: this.fb.array<FormControl<number | null>>([]),

    campDetail: this.fb.group({
      // Datepicker arbeitet mit Date | null
      startDate: this.fb.control<Date | null>(null),
      endDate: this.fb.control<Date | null>(null),
      description: this.fb.control<string>('', { nonNullable: true }),
      city: this.fb.control<string>('', { nonNullable: true }),
      // Array von Detail-Gruppen
      detailList: this.fb.array<FormGroup>([]),
    }),

    shownMaps: this.fb.control<any[] | null>(null),
    campZones: this.fb.control<any[] | null>(null),
  });

  // Getter für alle drei Steps (nicht‑null)
  get locationControl(): AbstractControl {
    return this.campForm.get('location') as AbstractControl;
  }

  get delimitationControl(): AbstractControl {
    return this.campForm.get('delimitation') as AbstractControl;
  }

  get nameControl(): AbstractControl {
    return this.campForm.get('name') as AbstractControl;
  }

  // convenience getters
  get detailList(): FormArray<FormGroup> {
    return this.campForm.get(['campDetail', 'detailList']) as FormArray<FormGroup>;
  }

  get subCamp(): FormArray<FormControl<number | null>> {
    return this.campForm.get('subCamp') as FormArray<FormControl<number | null>>;
  }

  get initialLocation(): GeoJsonFeatureDto | null {
    return this.campForm.value.location ?? null;
  }

  get initialZone(): Partial<CampZoneDto> | null {
    const zone = this.campForm.get('delimitation.zone')?.value;
    if (!zone) return null;
    return {
      zone,
      name: this.campForm.get('delimitation.name')?.value ?? 'Camp Delimitation',
      color: this.campForm.get('delimitation.color')?.value ?? '#ff0000',
      isCampDelimitation: this.campForm.get('delimitation.isCampDelimitation')?.value ?? true,
    };
  }

  onLocationPicked(location: GeoJsonFeatureDto): void {
    this.campForm.get('location')?.setValue(location);
    this.campForm.get('location')?.markAsDirty();
    this.campForm.get('location')?.markAsTouched();
  }

  onLocationConfirmed(location: GeoJsonFeatureDto): void {
    this.campForm.get('location')?.setValue(location);
    console.log("location confirmed", this.campForm.get('location')?.value);
  }

  onLocationPickCanceled(): void {
    // Optional: nichts tun, oder Dialog/Step zurück etc.
    // this.campForm.get('location')?.reset();
  }

  onZonePicked(zoneDto: CampZoneDto): void {
    this.campForm.patchValue({
      delimitation: {
        zone: zoneDto.zone,
        name: zoneDto.name,
        color: zoneDto.color,
        isCampDelimitation: zoneDto.isCampDelimitation
      }
    });
  }

  onZonePickCanceled(): void {
    // optional
  }

  addDetail() {
    const group = this.fb.group({
      title: this.fb.control<string>('', { nonNullable: true }),
      content: this.fb.control<string>('', { nonNullable: true }),
    });

    this.detailList.push(group);
  }

  removeDetail(index: number) {
    this.detailList.removeAt(index);
  }

  addSubCampId() {
    this.subCamp.push(this.fb.control<number | null>(null));
  }

  removeSubCamp(index: number) {
    this.subCamp.removeAt(index);
  }

  // build CreateCampDto and submit (replace console.log with service call)
  submit(): void {
    if (this.campForm.invalid) {
      this.campForm.markAllAsTouched();
      alert('Please fill all required fields');
      return;
    }

    const v = this.campForm.value;

    // validate required fields
    if (!v.name || !v.location) {
      alert('Missing required fields: name or location');
      return;
    }

    if (!v.delimitation?.zone) {
      alert('Missing required field: delimitation zone');
      return;
    }

    // build CampDetailDto
    const campDetail: CampDetailDto = {
      startDate: v.campDetail?.startDate
        ? new Date(v.campDetail.startDate).toISOString()
        : new Date().toISOString(),
      endDate: v.campDetail?.endDate
        ? new Date(v.campDetail.endDate).toISOString()
        : new Date().toISOString(),
      description: v.campDetail?.description ?? null,
      city: v.campDetail?.city ?? '',
      detailList:
        v.campDetail?.detailList && v.campDetail.detailList.length > 0
          ? v.campDetail.detailList.map((item: any) => ({
              title: item.title ?? '',
              content: item.content ?? '',
            }))
          : null,
    };

    // build CampZoneDto (delimitation)
    const delimitation: CampZoneDto = {
      name: v.delimitation.name ?? '',
      isCampDelimitation: v.delimitation.isCampDelimitation ?? true,
      color: v.delimitation.color ?? '#FF0000',
      zone: v.delimitation.zone as GeoJsonFeatureDto,
    };

    // build subCamp array (filter nulls/invalid)
    const subCamp: number[] = (v.subCamp ?? []).filter(
      (id): id is number => typeof id === 'number' && !isNaN(id)
    );

    // build shownMaps (optional)
    const shownMaps: string[] | null = v.shownMaps && v.shownMaps.length > 0 ? v.shownMaps : null;

    // build campZones (optional)
    const campZones: CampZoneDto[] | null =
      v.campZones && v.campZones.length > 0 ? v.campZones : null;

    // assemble final CreateCampDto
    const dto: CreateCampDto = {
      name: v.name,
      location: v.location as GeoJsonFeatureDto,
      delimitation,
      campDetail,
      isSubCamp: v.isSubCamp ?? false,
      parentId: v.parentId ?? 0,
      subCamp,
      shownMaps,
      campZones,
    };

    console.log('Sending CreateCampDto:', JSON.stringify(dto, null, 2));

    // Backend-Call
    this.isSubmitting = true;
    this.campService.createCamp(dto).subscribe({
      next: (createdCamp) => {
        console.log('Camp created successfully:', createdCamp);
        this.isSubmitting = false;
        // Navigate to home or camp detail
        this.router.navigate([`camp/${createdCamp.id}`]);
      },
      error: (err) => {
        console.error('Failed to create camp:', err);
        this.isSubmitting = false;
        const errorMsg = err.error?.message || err.message || 'Unknown error';
        alert(`Error creating camp: ${errorMsg}`);
      }
    });
  }

  startDelimitationEditor(): void {
    const sampleZone: GeoJsonFeatureDto = {
      type: 'Feature',
      geometry: {
        type: 'Polygon',
        coordinates: [
          [
            [7.444, 46.946],
            [7.450, 46.946],
            [7.450, 46.950],
            [7.444, 46.950],
            [7.444, 46.946]
          ]
        ]
      },
      properties: {}
    };
    this.campForm.get('delimitation.zone')?.setValue(sampleZone);
    this.campForm.get('delimitation.name')?.setValue('Hauptlager');
    this.campForm.get('delimitation.color')?.setValue('#FF0000');
    this.campForm.get('delimitation.isCampDelimitation')?.setValue(true);
  }
}
