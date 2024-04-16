import { Component, OnInit, Pipe, PipeTransform } from '@angular/core';

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


@Pipe({ name: 'nanToDash'}) // In Angular data to UI is transformed via Pipes
export class NanToDashPipe implements PipeTransform {
  transform(value: any): any {
    if (isNaN(value))
      return '-'; // Dash ('-') is a common convention to indicate a missing or invalid value. Nicer than writing the default 'NaN'.

    return value;
  }
}

// How to use typeof in Angular HTML? Answer: create a pipe instead of function call to get the typeof a variable.
// see : https://stackoverflow.com/questions/37511055/how-to-check-type-of-variable-in-ngif-in-angular2
// We prefer the Pipe version solution. The version of the 'a helper method in the component' is less reusable.
// Because we have to write that helper function in every component we use. Instead, a pipe version once it is written can be used anywhere without bloating the component itself.
@Pipe({ name: 'sqTypeOf'})
export class TypeOfPipe implements PipeTransform {
  transform(value: any): any {
    return typeof value;
  }
}