import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SqTreeViewComponent } from './sq-tree-view.component';

describe('SqTreeViewComponent', () => {
  let component: SqTreeViewComponent;
  let fixture: ComponentFixture<SqTreeViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ SqTreeViewComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(SqTreeViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
