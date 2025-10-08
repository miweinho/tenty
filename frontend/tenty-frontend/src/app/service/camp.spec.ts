import { TestBed } from '@angular/core/testing';

import { Camp } from './camp';

describe('Camp', () => {
  let service: Camp;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Camp);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
