import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PickLocationMapViewComponent } from './pick-location-map-view';

describe('PickLocationMapView', () => {
  let component: PickLocationMapViewComponent;
  let fixture: ComponentFixture<PickLocationMapViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PickLocationMapViewComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PickLocationMapViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
