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

  public static splitStrToMulLines(p_str:string) : string {
      var chunks: string[] = [];
      for (var i = 0; i < p_str.length; i += 250) {
        chunks.push(p_str.substring(i, i + 250));
      }
      return chunks.join("\n");
  }

  constructor() { }

  ngOnInit(): void {
  }


}
