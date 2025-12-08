import { TestBed } from '@angular/core/testing';

import { CampMapHelperService } from './camp-map-helper';

describe('CampMapHelper', () => {
  let service: CampMapHelperService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CampMapHelperService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
