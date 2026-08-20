import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'lib-sq-ng-common',
  template: `
  <p>
    sq-ng-common works!
  </p>
  `,
  styles: [],
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false
})
export class SqNgCommonComponent implements OnInit {
  constructor() { }

  ngOnInit(): void {
  }
}