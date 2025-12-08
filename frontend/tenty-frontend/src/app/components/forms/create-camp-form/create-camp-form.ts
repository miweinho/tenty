import { Component, inject } from '@angular/core';
import { FormBuilder, FormArray, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatStepperModule } from '@angular/material/stepper';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatIconModule } from '@angular/material/icon';
import { GeoJsonFeatureDto, CampZoneDto, CampDetailDto, CreateCampDto } from '../../../models';

@Component({
  selector: 'app-create-camp-form',
  standalone: true,
  imports: [
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatIconModule,
    ReactiveFormsModule
  ],
  templateUrl: './create-camp-form.html',
  styleUrls: ['./create-camp-form.css']
})
export class CreateCampForm {
  private fb = inject(FormBuilder);

  // form structure
  campForm = this.fb.group({
    // step 1: location
    // explicit typed control so setValue accepts GeoJsonFeatureDto
    location: this.fb.control<GeoJsonFeatureDto | null>(null, { nonNullable: false, validators: Validators.required }),

    // step 2: delimitation
    delimitation: this.fb.group({
      name: [''],
      isCampDelimitation: [true],
      color: ['#ff0000'],
      // typed control for zone
      zone: this.fb.control<GeoJsonFeatureDto | null>(null)
    }),

    // step 3: rest
    name: ['', Validators.required],
    isSubCamp: [false],
    parentId: [null],
    subCamp: this.fb.array<string | number>([]),
    campDetail: this.fb.group({
      startDate: [null],
      endDate: [null],
      description: [''],
      city: [''],
      detailList: this.fb.array([])
    }),
    shownMaps: [[]],
    campZones: [[]]
  });

  // convenience getters
  get detailList(): FormArray {
    return this.campForm.get(['campDetail', 'detailList']) as FormArray;
  }
  get subCamp(): FormArray {
    return this.campForm.get('subCamp') as FormArray;
  }

  // placeholder: open location picker (replace with real picker)
  startLocationPicker(): void {
    // TODO:.open map modal / component and set location control to selected GeoJsonFeatureDto
    // Example placeholder value (Point)
    const sample: GeoJsonFeatureDto = {
      type: 'Feature',
      geometry: { type: 'Point', coordinates: [7.44, 46.95] },
      properties: {}
    };
    this.campForm.get('location')?.setValue(sample);
  }

  // placeholder: start delimitation drawing
  startDelimitationEditor(): void {
    // TODO: open delimitation editor component and set delimitation.zone with resulting GeoJsonFeatureDto
    const sampleZone: GeoJsonFeatureDto = {
      type: 'Feature',
      geometry: { type: 'Polygon', coordinates: [[[7.44,46.95],[7.441,46.95],[7.441,46.951],[7.44,46.95]]] },
      properties: {}
    };
    // include isCampDelimitation because the FormGroup's typing requires it
    this.campForm.get('delimitation')?.patchValue({
      name: 'Delim',
      isCampDelimitation: true,
      zone: sampleZone,
      color: '#ff0000'
    });
  }

  addDetail() {
    // use simple initial values (safer than array syntax inside group)
    this.detailList.push(this.fb.group({ title: '', content: '' }));
  }

  removeDetail(index: number) {
    this.detailList.removeAt(index);
  }

  addSubCampId() {
    this.subCamp.push(this.fb.control(null));
  }

  removeSubCamp(index: number) {
    this.subCamp.removeAt(index);
  }

  // build CreateCampDto and submit (replace console.log with service call)
  submit(): void {
    if (this.campForm.invalid) {
      this.campForm.markAllAsTouched();
      return;
    }

    const v = this.campForm.value as Partial<CreateCampDto>;

    if (!v.name || !v.location) {
      console.error('Missing required fields', { name: v.name, location: v.location });
      return;
    }

    const dto: CreateCampDto = {
      name: v.name!,
      location: v.location as GeoJsonFeatureDto,
      delimitation: {
        name: v.delimitation?.name ?? '',
        isCampDelimitation: !!v.delimitation?.isCampDelimitation,
        color: v.delimitation?.color ?? '#ff0000',
        zone: v.delimitation?.zone as any
      } as CampZoneDto,
      campDetail: (v.campDetail ?? {}) as CampDetailDto,
      isSubCamp: !!v.isSubCamp,
      parentId: v.parentId ?? 0,
      subCamp: v.subCamp ?? [],
      shownMaps: v.shownMaps ?? null,
      campZones: v.campZones ?? null
    };

    console.log('Submitting DTO', dto);
  }
}
