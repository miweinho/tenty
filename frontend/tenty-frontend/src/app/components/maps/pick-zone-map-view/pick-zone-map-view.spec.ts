import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PickZoneMapView } from './pick-zone-map-view';

describe('PickZoneMapView', () => {
  let component: PickZoneMapView;
  let fixture: ComponentFixture<PickZoneMapView>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PickZoneMapView]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PickZoneMapView);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
