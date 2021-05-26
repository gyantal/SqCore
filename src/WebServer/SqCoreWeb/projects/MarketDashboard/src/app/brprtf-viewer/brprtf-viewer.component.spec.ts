import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { BrPrtfViewerComponent } from './brprtf-viewer.component';

describe('BrPrtfViewerComponent', () => {
  let component: BrPrtfViewerComponent;
  let fixture: ComponentFixture<BrPrtfViewerComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ BrPrtfViewerComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(BrPrtfViewerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
