import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, inject } from '@angular/core';
import {
  FormBuilder,
  FormArray,
  FormControl,
  FormGroup,
  Validators,
  ReactiveFormsModule,
  AbstractControl,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import {
  GeoJsonFeatureDto,
  CampZoneDto,
  CampDetailDto,
  UpdateCampDto,
  FullCampDto,
} from '../../../models';
import { MatCardModule } from '@angular/material/card';
import { MatStepperModule } from '@angular/material/stepper';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { CampService } from '../../../service/camps/camp';
import { PickLocationMapViewComponent } from '../../maps/pick-location-map-view/pick-location-map-view';
import { PickZoneMapView } from '../../maps/pick-zone-map-view/pick-zone-map-view';

@Component({
  selector: 'app-show-camp-form',
  standalone: true,
  imports: [
    MatCardModule,
    MatStepperModule,
    MatIconModule,
    MatFormFieldModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatNativeDateModule,
    ReactiveFormsModule,
    CommonModule,
    MatInputModule,
    MatListModule,
    PickLocationMapViewComponent,
    PickZoneMapView,
  ],
  templateUrl: './show-camp-form.html',
  styleUrls: ['./show-camp-form.css'],
})
export class ShowCampFormComponent implements OnChanges {
  @Input() camp: FullCampDto | null = null;
  @Output() saved = new EventEmitter<FullCampDto>();

  private fb = inject(FormBuilder);
  private campService = inject(CampService);

  campForm = this.fb.group({
    location: this.fb.control<GeoJsonFeatureDto | null>(null, { validators: Validators.required }),
    delimitation: this.fb.group({
      name: this.fb.control<string>('', { nonNullable: true }),
      isCampDelimitation: this.fb.control<boolean>(true, { nonNullable: true }),
      color: this.fb.control<string>('#ff0000', { nonNullable: true }),
      zone: this.fb.control<GeoJsonFeatureDto | null>(null, { validators: Validators.required }),
    }),
    name: this.fb.control<string>('', { nonNullable: true, validators: Validators.required }),
    isSubCamp: this.fb.control<boolean>(false, { nonNullable: true }),
    parentId: this.fb.control<number | null>(null),
    subCamp: this.fb.array<FormControl<number | null>>([]),
    campDetail: this.fb.group({
      startDate: this.fb.control<Date | null>(null),
      endDate: this.fb.control<Date | null>(null),
      description: this.fb.control<string>('', { nonNullable: true }),
      city: this.fb.control<string>('', { nonNullable: true }),
      detailList: this.fb.array<FormGroup>([]),
    }),
    shownMaps: this.fb.control<any[] | null>(null),
    campZones: this.fb.control<any[] | null>(null),
  });

  get locationControl(): AbstractControl {
    return this.campForm.get('location') as AbstractControl;
  }
  get delimitationControl(): AbstractControl {
    return this.campForm.get('delimitation') as AbstractControl;
  }
  get nameControl(): AbstractControl {
    return this.campForm.get('name') as AbstractControl;
  }
  get detailList(): FormArray<FormGroup> {
    return this.campForm.get(['campDetail', 'detailList']) as FormArray<FormGroup>;
  }
  get subCamp(): FormArray<FormControl<number | null>> {
    return this.campForm.get('subCamp') as FormArray<FormControl<number | null>>;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['camp'] && this.camp) {
      this.patchFromCamp(this.camp);
    }
  }

  private patchFromCamp(camp: FullCampDto): void {
    this.campForm.patchValue({
      location: camp.location,
      delimitation: {
        name: camp.delimitation?.name ?? '',
        isCampDelimitation: camp.delimitation?.isCampDelimitation ?? true,
        color: camp.delimitation?.color ?? '#ff0000',
        zone: camp.delimitation?.zone ?? null,
      },
      name: camp.name ?? '',
      isSubCamp: camp.isSubCamp ?? false,
      parentId: camp.parentId ?? null,
      campDetail: {
        startDate: camp.campDetail?.startDate ? new Date(camp.campDetail.startDate) : null,
        endDate: camp.campDetail?.endDate ? new Date(camp.campDetail.endDate) : null,
        description: camp.campDetail?.description ?? '',
        city: camp.campDetail?.city ?? '',
      },
    });
    this.subCamp.clear();
    (camp.subCamp ?? []).forEach((id) => this.subCamp.push(this.fb.control<number | null>(id)));
  }

  onLocationPicked(location: GeoJsonFeatureDto): void {
    this.campForm.get('location')?.setValue(location);
    this.campForm.get('location')?.markAsDirty();
    this.campForm.get('location')?.markAsTouched();
  }

  onZonePicked(zone: any): void {
    const feature = zone.feature ?? zone;
    const name = zone.name ?? this.campForm.get(['delimitation', 'name'])?.value ?? 'delim';
    const color = zone.color ?? this.campForm.get(['delimitation', 'color'])?.value ?? '#ff0000';
    this.campForm.get(['delimitation', 'zone'])?.setValue(feature);
    this.campForm.get(['delimitation', 'name'])?.setValue(name);
    this.campForm.get(['delimitation', 'color'])?.setValue(color);
    this.campForm.get(['delimitation', 'zone'])?.markAsDirty();
    this.campForm.get(['delimitation', 'zone'])?.markAsTouched();
  }

  submit(): void {
    if (!this.camp || this.campForm.invalid) {
      this.campForm.markAllAsTouched();
      return;
    }
    const v = this.campForm.value;
    if (!v.name || !v.location || !v.delimitation?.zone) return;

    const campDetail: CampDetailDto = {
      startDate: v.campDetail?.startDate ? new Date(v.campDetail.startDate).toISOString() : new Date().toISOString(),
      endDate: v.campDetail?.endDate ? new Date(v.campDetail.endDate).toISOString() : new Date().toISOString(),
      description: v.campDetail?.description ?? null,
      city: v.campDetail?.city ?? '',
      detailList: v.campDetail?.detailList ?? null,
    };

    const delimitation: CampZoneDto = {
      name: v.delimitation.name ?? '',
      isCampDelimitation: v.delimitation.isCampDelimitation ?? true,
      color: v.delimitation.color ?? '#FF0000',
      zone: v.delimitation.zone as GeoJsonFeatureDto,
    };

    const dto: UpdateCampDto = {
      name: v.name,
      location: v.location as GeoJsonFeatureDto,
      delimitation,
      campDetail,
      isSubCamp: v.isSubCamp ?? false,
      parentId: v.parentId ?? 0,
    };

    this.campService.updateCamp(this.camp.id, dto).subscribe({
      next: (updated) => {
        this.saved.emit(updated);
      },
      error: (err) => console.error('Failed to update camp', err),
    });
  }
}
