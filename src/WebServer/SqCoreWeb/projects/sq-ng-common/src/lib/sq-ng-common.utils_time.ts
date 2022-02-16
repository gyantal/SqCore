import { Component, OnInit } from '@angular/core';

export const minDate = new Date(-8640000000000000);

@Component({
  selector: 'lib-sq-ng-common',
  template: `
    <p>
      sq-ng-common works!
    </p>
  `,
  styles: []
})
export class SqNgCommonUtilsTime implements OnInit {
  constructor() { }

  ngOnInit(): void {
  }

  // Javascript doesn't support timezones, so either use moment.js or hack it here.
  // https://stackoverflow.com/questions/36206260/how-to-set-date-always-to-eastern-time-regardless-of-users-time-zone/36206597
  public static ConvertDateUtcToEt(utcDate: Date) {
    const monthOriUTC = utcDate.getMonth() + 1;
    const dayOriUTC = utcDate.getDate();
    const dayOfWeekOriUTC = utcDate.getDay(); // Sunday is 0, Monday is 1, and so on.

    // https://en.wikipedia.org/wiki/Eastern_Time_Zone
    // on the second Sunday in March, at 2:00 a.m. EST, clocks are advanced to 3:00 a.m. EDT leaving a one-hour "gap". On the first Sunday in November, at 2:00 a.m. EDT,
    // clocks are moved back to 1:00 a.m. EST, thus "duplicating" one hour. Southern parts of the zone (Panama and the Caribbean) do not observe daylight saving time.""
    let offsetToNYTime = -4;
    if (monthOriUTC < 3 || monthOriUTC === 12 ||
      (monthOriUTC === 3 && (dayOriUTC - dayOfWeekOriUTC) < 8) ||
      (monthOriUTC === 11 && (dayOriUTC - dayOfWeekOriUTC) >= 1)) { // (dayOriUTC - dayOfWeekOriUTC) is the date of the previous Sunday from today. If 1st of November = Sunday, on 2nd Nov, Monday, offset should be -5
      offsetToNYTime = -5;
    }
    const dateEt: Date = utcDate;
    dateEt.setTime(dateEt.getTime() + offsetToNYTime * 60 * 60000);
    return dateEt;
  }

  public static ConvertDateUtcToLoc(utcDate: Date) {
    // Maybe this will help.
    // uiSnapTable.snapLastUpateTime = new Date(brAccSnap.lastUpdate);  // if the string contains "Z" time zone postfix, then JS converts the result to Local time

    // not sure it works. check it later.
    // const monthOriUTC = utcDate.getMonth() + 1;
    // const dayOriUTC = utcDate.getDate();
    // const dayOfWeekOriUTC = utcDate.getDay(); // Sunday is 0, Monday is 1, and so on.

    // // https://en.wikipedia.org/wiki/Eastern_Time_Zone
    // // on the second Sunday in March, at 2:00 a.m. EST, clocks are advanced to 3:00 a.m. EDT leaving a one-hour "gap". On the first Sunday in November, at 2:00 a.m. EDT,
    // // clocks are moved back to 1:00 a.m. EST, thus "duplicating" one hour. Southern parts of the zone (Panama and the Caribbean) do not observe daylight saving time.""
    // let offsetToNYTime = 5.30;
    // if (monthOriUTC < 3 || monthOriUTC === 12
    //   || (monthOriUTC === 3 && (dayOriUTC - dayOfWeekOriUTC) < 8)
    //   || (monthOriUTC === 11 && (dayOriUTC - dayOfWeekOriUTC) >= 1)) {  // (dayOriUTC - dayOfWeekOriUTC) is the date of the previous Sunday from today. If 1st of November = Sunday, on 2nd Nov, Monday, offset should be -5
    //   offsetToNYTime = 4.30;
    // }
    // const dateLoc: Date = utcDate;
    // dateLoc.setTime(dateLoc.getTime() + offsetToNYTime * 60 * 60000);
    // return dateLoc;
  }

  // https://stackoverflow.com/questions/948532/how-do-you-convert-a-javascript-date-to-utc    "a method I've been using many times. function convertDateToUTC(date) {..."
  public static ConvertDateLocToEt(locDate: Date) {
    const dateUtc = new Date(locDate.getUTCFullYear(), locDate.getUTCMonth(), locDate.getUTCDate(), locDate.getUTCHours(), locDate.getUTCMinutes());
    return this.ConvertDateUtcToEt(dateUtc);
  }

  // https://stackoverflow.com/questions/542938/how-do-i-get-the-number-of-days-between-two-dates-in-javascript
  public static DateDiffNdays(startDate: Date, endDate: Date) {
    // Take the difference between the dates and divide by milliseconds per day.
    // Round to nearest whole number to deal with DST.
    return Math.round((endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24));
  }

  // zeroPad = (num, places: number) => String(num).padStart(places, '0');  // https://stackoverflow.com/questions/2998784/how-to-output-numbers-with-leading-zeros-in-javascript
  // ES5 approach: because 2021-02: it works in CLI, but VsCode shows problems: "Property 'padStart' does not exist on type 'string'. Do you need to change your target library? Try changing the `lib` compiler option to 'es2017' or later."
  public static zeroPad(num, places) {
    const zero = places - num.toString().length + 1;
    return Array(+(zero > 0 && zero)).join('0') + num;
  }

  public static Date2PaddedIsoStr(date: Date): string { // 2020-9-1 is not acceptable. Should be converted to 2020-09-01
    // don't use UTC versions, because they will convert local time zone dates to UTC first, then we might have bad result.
    // "date = 'Tue Apr 13 2021 00:00:00 GMT+0100 (British Summer Time)'" because local BST is not UTC date.getUTCDate() = 12, while date.getDate()=13 (correct)
    // return this.zeroPad(date.getUTCFullYear(), 4) + '-' + this.zeroPad(date.getUTCMonth() + 1, 2) + '-' + this.zeroPad(date.getUTCDate(), 2);
    return this.zeroPad(date.getFullYear(), 4) + '-' + this.zeroPad(date.getMonth() + 1, 2) + '-' + this.zeroPad(date.getDate(), 2);
  }

  public static PaddedIsoStr3Date(dateStr: string): Date {
    const parts = dateStr.split('-');
    const year = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10);
    const day = parseInt(parts[2], 10);
    return new Date(year, month - 1, day);
  }

  public static getTimespanStr(dateFrom: Date, dateTo: Date): string {
    return ((dateFrom === minDate || dateTo === minDate) ? 'NaN' : (dateTo.getTime() - dateFrom.getTime()) + 'ms');
  }

  public static ConvertMilliSecToTimeStr(totalMilliSec: number): string {
    // let milliseconds = totalMilliSec % 1000;
    let seconds = Math.floor((totalMilliSec / 1000) % 60).toString();
    let minutes = Math.floor((totalMilliSec / (60 * 1000)) % 60).toString();
    let hours = Math.floor((totalMilliSec / (3600 * 1000)) % 60).toString();
    let days = Math.floor((totalMilliSec/ (24 * 3600 * 1000)) % 60).toString();

    if (days.length < 2) days = '0' + days;
    if (hours.length < 2) hours = '0' + hours;
    if (minutes.length < 2) minutes= '0' + minutes;
    if (seconds.length < 2) seconds = '0' + seconds;

    if (days == '00' && hours == '00' && minutes == '00' && seconds == '00')
      return '0s ago';
    else if (days == '00' && hours == '00' && minutes == '00')
      return seconds + 's ago';
    else if (days == '00' && hours == '00')
      return minutes + 'm' + ' ' + seconds + 's ago';
    else if (days == '00')
      return hours + 'h' + ' ' + minutes + 'm' + ' ' + seconds + 's ago';
    else return days + 'd' + hours + 'h' + ' ' + minutes + 'm' + ' ' + seconds + 's ago';
  }

  public static CheckInputDateIsValidOrNot(date: string) {
    let isGoodDate = !(date === '');
    const parts = date.split('-');
    const year = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10);
    const day = parseInt(parts[2], 10);
    if (year < 1980 || year > 2040)
      isGoodDate = false;
    if (month < 1 || month > 12)
      isGoodDate = false;
    if (day < 1 || day > 31)
      isGoodDate = false;
    if (!isGoodDate)
      return;
  }
}