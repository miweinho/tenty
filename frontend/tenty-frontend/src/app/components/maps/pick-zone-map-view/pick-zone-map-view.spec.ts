import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PickZoneMapViewComponent } from './pick-zone-map-view';

describe('PickZoneMapView', () => {
  let component: PickZoneMapViewComponent;
  let fixture: ComponentFixture<PickZoneMapViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PickZoneMapViewComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PickZoneMapViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
