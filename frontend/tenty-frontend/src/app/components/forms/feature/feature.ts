import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-feature',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './feature.html',
  styleUrl: './feature.css',
})
export class Feature {
  @Input() featureType: string | null = null;
  @Output() featureCreated = new EventEmitter<any>();
  
  featureForm: FormGroup;
  featureTypes = ['tent', 'water', ''];

  constructor(private fb: FormBuilder) {
    this.featureForm = this.fb.group({
      featureType: ['', Validators.required],
      name: ['', Validators.required],
      // Tent-spezifische Felder
      length: [10],
      width: [8],
      orientation: [0],
      color: ['#ffffff']
    });
  }

  ngOnInit() {
    // Wenn featureType übergeben wurde, setze ihn im Form
    if (this.featureType) {
      this.featureForm.patchValue({ featureType: this.featureType });
    }
  }

  // Hilfsmethode für bedingte Anzeige
  isFeatureType(type: string): boolean {
    return this.featureForm.get('featureType')?.value === type;
  }

  onSubmit() {
    if (this.featureForm.valid) {
      const formData = this.featureForm.value;
      const featureData = {
        kind: formData.featureType.toLowerCase(),
        name: formData.name,
        lengthM: formData.length,
        widthM: formData.width,
        headingDeg: formData.orientation,
        color: formData.color
      };
      
      console.log('Feature created:', featureData);
      this.featureCreated.emit(featureData);
    }
  }
}
