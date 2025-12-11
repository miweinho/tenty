import { TestBed } from '@angular/core/testing';

import { CampService } from './camp';

describe('Camp', () => {
  let service: CampService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CampService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
