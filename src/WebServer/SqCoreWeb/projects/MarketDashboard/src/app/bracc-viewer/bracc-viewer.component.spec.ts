import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { BrAccViewerComponent } from './bracc-viewer.component';

describe('BrAccViewerComponent', () => {
  let component: BrAccViewerComponent;
  let fixture: ComponentFixture<BrAccViewerComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ BrAccViewerComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(BrAccViewerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
