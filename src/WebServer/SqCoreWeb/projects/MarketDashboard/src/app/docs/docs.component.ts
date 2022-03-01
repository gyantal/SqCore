import { Component, OnInit, OnChanges, SimpleChanges, Input } from '@angular/core';

// 2020-05:
// Creating a separate components for Doc's WhatIsNow / GetStarted / Tutorial is always an option, but that would bloat the code with multiple CSS, TS, HTML
// - typescript's 'import' html as string: not possible. It wants to import as module.
// - Angular: multiple templateUrl files in Module are not possible
// - finally solved it with 3 Angular components, but in one TS file.

@Component({
  selector: 'app-docs-what-is-new',
  templateUrl: './docs-what-is-new.html',
  styleUrls: ['./docs.component.scss']
})
export class DocsWhatIsNewComponent implements OnInit, OnChanges {
  @Input() _parentActiveTool?: string = undefined; // this property will be input from above parent container

  constructor() { }

  ngOnInit(): void {
  }

  ngOnChanges(changes: SimpleChanges) {
    console.log('DocsComponent:ngOnChanges(): ' + changes._parentActiveTool.currentValue); // or previousValue
  }
}


@Component({
  selector: 'app-docs-get-started',
  templateUrl: './docs-get-started.html',
  styleUrls: ['./docs.component.scss']
})
export class DocsGetStartedComponent implements OnInit, OnChanges {
  @Input() _parentActiveTool?: string = undefined; // this property will be input from above parent container

  constructor() { }

  ngOnInit(): void {
  }

  ngOnChanges(changes: SimpleChanges) {
    console.log('DocsComponent:ngOnChanges(): ' + changes._parentActiveTool.currentValue); // or previousValue
  }
}


@Component({
  selector: 'app-docs-tutorial',
  templateUrl: './docs-tutorial.html',
  styleUrls: ['./docs.component.scss']
})
export class DocsTutorialComponent implements OnInit, OnChanges {
  @Input() _parentActiveTool?: string = undefined; // this property will be input from above parent container

  constructor() { }

  ngOnInit(): void {
  }

  ngOnChanges(changes: SimpleChanges) {
    console.log('DocsComponent:ngOnChanges(): ' + changes._parentActiveTool.currentValue); // or previousValue
  }
}