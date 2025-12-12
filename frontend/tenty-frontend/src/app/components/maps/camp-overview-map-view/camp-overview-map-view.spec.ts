import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CampOverviewMapViewComponent } from './camp-overview-map-view';

describe('CampOverviewMapView', () => {
  let component: CampOverviewMapViewComponent;
  let fixture: ComponentFixture<CampOverviewMapViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CampOverviewMapViewComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CampOverviewMapViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
