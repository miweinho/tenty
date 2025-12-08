import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CreateCampForm } from './create-camp-form';

describe('CreateCampForm', () => {
  let component: CreateCampForm;
  let fixture: ComponentFixture<CreateCampForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateCampForm]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CreateCampForm);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
