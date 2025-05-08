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

    return value.toFixed(2); // Formatting the number to 2 decimal places, so we don't need to use Angular's number pipe (e.g., number:'1.2-2') in the HTML. Example: 54.65555 becomes '54.65'.
  }
}

@Pipe({ name: 'nanToDashPct'}) // In Angular data to UI is transformed via Pipes, special case for Seasonality data
export class NanToDashPctPipe implements PipeTransform {
  transform(value: any): any {
    if (isNaN(value))
      return '-'; // Dash ('-') is a common convention to indicate a missing or invalid value. Nicer than writing the default 'NaN'.

    return (value * 100).toFixed(2) + '%'; // Convert the value to a percentage and format it with two decimal places
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

@Pipe({ name: 'numberToTBMK' }) // Transform a number into a formatted string based on its magnitude
export class NumberToTBMKPipe implements PipeTransform {
  // @param input - The number to be formatted.
  // @param args - Optional argument to specify the number of decimal places for formatting.
  // For example, if you use the pipe like this: {{ numberValue | numberToTBMK: 2 }}, it means that the number should be formatted with 2 decimal places. The args in this case will be 2, and input.toFixed(args) will format the number accordingly.
  transform(input: any, args?: any): any {
    if (Number.isNaN(input))
      return input;

    // Format the number based on its magnitude
    if (input > 1e12)
      return (input / 1e12).toFixed(3) + 'T'; // Format for Trillion
    else if (input > 1e11)
      return (input / 1e9).toFixed(0) + 'B'; // Format for Billion without decimals. Numbers between 100B..999B => e.g. "613B"
    else if (input > 1e10)
      return (input / 1e9).toFixed(1) + 'B'; // Format for Billion with one decimal place. Numbers between 10B..99B => e.g. "61.3B
    else if (input > 1e9)
      return (input / 1e9).toFixed(2) + 'B'; // Format for Billion with two decimal places. Numbers between 1B..9.99B => e.g. "6.13B"
    else if (input > 1e8)
      return (input / 1e6).toFixed(0) + 'M'; // Format for Million without decimals. Numbers between 100M..999M => e.g. "613M"
    else if (input > 1e7)
      return (input / 1e6).toFixed(1) + 'M'; // Format for Million with one decimal place. Numbers between 10M..99M => e.g. "61.3M"
    else if (input > 1e6)
      return (input / 1e6).toFixed(2) + 'M'; // Format for Million with two decimal places. Numbers between 1M..9.99M => e.g. "6.13M"
    else if (input > 1e5)
      return (input / 1e3).toFixed(0) + 'K'; // Format for Thousand without decimal. Numbers between 100K..999K => e.g. "613K"
    else if (input > 1e4)
      return (input / 1e3).toFixed(1) + 'K'; // Format for Thousand with one decimal place. Numbers between 10K..99K => e.g. "61.3K"
    else if (input > 1e3)
      return (input / 1e3).toFixed(2) + 'K'; // Format for Thousand with two decimal places. Numbers between 1K..9.99K => e.g. "6.13K"
    else
      return input.toFixed(args); // Format with the specified number of decimal places
  }
}