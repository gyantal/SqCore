import { Component, OnInit } from '@angular/core';

@Component({
  selector: 'lib-sq-ng-common',
  template: `
    <p>
      sq-ng-common works!
    </p>
  `,
  styles: []
})
export class SqNgCommonUtilsStr implements OnInit {
  public static splitStrToMulLines(str:string) : string {
    const chunks: string[] = [];
    for (let i = 0; i < str.length; i += 250)
      chunks.push(str.substring(i, i + 250));
    return chunks.join('\n');
  }

  constructor() { }

  ngOnInit(): void {
  }
}