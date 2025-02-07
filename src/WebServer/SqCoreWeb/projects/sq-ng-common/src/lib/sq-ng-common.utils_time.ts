import { Component, OnInit } from '@angular/core';

export const minDate = new Date(-8640000000000000);
export const maxDate = new Date(8640000000000000);

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

  // 2020: Javascript didn't support timezones, so either use moment.js or hack it here.
  // https://stackoverflow.com/questions/36206260/how-to-set-date-always-to-eastern-time-regardless-of-users-time-zone/36206597
  public static ConvertDateUtcToEt(utcDate: Date) : Date {
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

  // What is the behavior of JavaScript's Date object regarding time zones?
  // Answer: JavaScript's Date object tracks time internally in UTC but typically displays (even in Debug Watch!) and accepts input based on the local time of the computer it's running on. While you can set it to a different timezone, methods like toLocaleString() will still show the time in the local timezone.
  // See: https://stackoverflow.com/questions/15141762/how-to-initialize-a-javascript-date-to-a-particular-time-zone , https://stackoverflow.com/questions/439630/create-a-date-with-a-set-timezone-without-using-a-string-representation/439871#439871
  public static ConvertDateEtToUtc(inputDateImplicitUtc: Date): Date {
    // Based on the input p_date the timezone difference can be 4 or 5 hours depending on daylight saving. E.g. ET to UTC difference: 2024-05-16 (summer-time): 5h, 2024-01-02 (winter-time): 4h
    // Trick: if "timeZoneName: 'short'" is not given, it doesn't add the timezone postfix string to the end. We will use this trick to omit the TimeZoneInfo from the string. See TestDateToLocaleString() function implementation.
    const inputDateToUtcWithoutTimeZoneStr: string = inputDateImplicitUtc.toLocaleString('en-US', { timeZone: 'UTC' }); // "4/16/2024, 9:40:49 PM UTC", postfix omitted => "4/16/2024, 9:40:49 PM"
    const inputDateToEtWithoutTimeZoneStr: string = inputDateImplicitUtc.toLocaleString('en-US', { timeZone: 'America/New_York' }); // "4/16/2024, 5:40:49 PM EDT", postfix omitted => "4/16/2024, 5:40:49 PM"
    // Then we interpret these broken strings (not containing the TimeZoneInfo postfix string) as fake LocalDate strings. With the new Date("<MyLocalDateString>") constructor.
    const brokenUtcStrAsLocalDate = new Date(inputDateToUtcWithoutTimeZoneStr);
    const brokenEtStrAsLocalDate = new Date(inputDateToEtWithoutTimeZoneStr);
    // Calculate offset from ET to UTC
    const offsetEtToUtc: number = Math.abs(Math.floor((brokenEtStrAsLocalDate.getTime() - brokenUtcStrAsLocalDate.getTime()) / (1000 * 60 * 60))); // divide my msec (1000), sec (60), min (60) to get the difference in hours
    // console.log('offsetEtToUtc: ', offsetEtToUtc);

    const etHours: number = inputDateImplicitUtc.getHours();
    const newHours: number = etHours + offsetEtToUtc; // Calculate new hours in UTC
    inputDateImplicitUtc.setHours(newHours);
    return inputDateImplicitUtc;
  }

  public static ConvertDateUtcToLoc() {
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

  public static DateTime2PaddedIsoStr(date: Date): string { // converting a Date : "Thu Jan 18 2024 18:30:00 GMT+0530 (India Standard Time)" => "2024-01-18T18:30:00"
    // The JavaScript date.toISOString() method creates a date string in the format "2024-01-18T18:30:00.000Z". However, we need a customized date format without timezone information for storing the date on the server, like "2024-01-18T18:30:00"
    return this.zeroPad(date.getFullYear(), 4) + '-' + this.zeroPad(date.getMonth() + 1, 2) + '-' + this.zeroPad(date.getDate(), 2)+ 'T' + this.zeroPad(date.getHours(), 2) + ':' + this.zeroPad(date.getMinutes(), 2) + ':' + this.zeroPad(date.getSeconds(), 2);
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

  public static ConvertMilliSecToTimeStr(totalMilliSec: number): string { // 5380 => '05s ago'
    if (totalMilliSec < 0) { // if input is negative, output is -1 values. -1518 => '-1d-1h -1m -2s ago'
      // totalMilliSec = 0; // don't fix negative values to 0, because this might hide unexpected errors. If input is negative, better to find the reason and fix that.
      console.warn('ConvertMilliSecToTimeStr(), negative input ' + totalMilliSec + '. Check https://time.is/ . If your clock is behind, use timedate.cpl/InternetTime/Synch With time server');
    }

    // let milliseconds = totalMilliSec % 1000;
    let seconds = Math.floor((totalMilliSec / 1000) % 60).toString();
    let minutes = Math.floor((totalMilliSec / (60 * 1000)) % 60).toString(); // Math.floor() function always rounds down, so a small near zero negative number is converted to -1
    let hours = Math.floor((totalMilliSec / (3600 * 1000)) % 24).toString();
    let days = Math.floor(totalMilliSec / (24 * 3600 * 1000)).toString();

    if (days.length < 2) days = '0' + days;
    if (hours.length < 2) hours = '0' + hours;
    if (minutes.length < 2) minutes= '0' + minutes;
    if (seconds.length < 2) seconds = '0' + seconds;

    let result = '';
    if (days == '00' && hours == '00' && minutes == '00' && seconds == '00')
      result = '0s ago';
    else if (days == '00' && hours == '00' && minutes == '00')
      result = seconds + 's ago';
    else if (days == '00' && hours == '00')
      result = minutes + 'm' + ' ' + seconds + 's ago';
    else if (days == '00')
      result = hours + 'h' + ' ' + minutes + 'm' + ' ' + seconds + 's ago';
    else
      result = days + 'd' + hours + 'h' + ' ' + minutes + 'm' + ' ' + seconds + 's ago';

    // console.log('ConvertMilliSecToTimeStr(), input => output: ' + totalMilliSec + ' => ' + result);
    return result;
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

  public static RemoveHyphensFromDateStr(dateStr: string): string { // Date : "2024-03-13"(yyyy-MM-dd) to "20240313"(yyyyMMdd)
    return dateStr.split('-').join('');
  }

  public static ValidateDateStr(tradeDtStr: string, rowInd: number): string | null { // DateStr : NOV 14 21:00:03 or 21:00:03
    const dateParts: string[] = tradeDtStr.split(' ');

    if (dateParts.length == 1) // Handles cases where dateParts contains only the time part (e.g., 21:00:03) or an invalid date format (e.g., NOV2504:60:03).
      return this.ValidateTimeStr(tradeDtStr, rowInd);

    // Validate month
    const validMonths: string[] = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];
    const month: string = dateParts[0];
    if (!validMonths.includes(month))
      return `Error. invalid month: ${month} at row ${rowInd + 1}`;
    // Validate day
    const day = parseInt(dateParts[1], 10);
    if (day < 1 || day > 31)
      return `Error. invalid day: ${day} at row ${rowInd + 1}`;
    // Validate time
    return this.ValidateTimeStr(dateParts[2], rowInd);
  }

  public static ValidateTimeStr(timeStr: string, rowInd: number): string | null { // e.g: 21:00:03 or NOV2504:00:03
    const timeParts: string[] = timeStr.split(':');
    const hours = parseInt(timeParts[0], 10);
    const minutes = parseInt(timeParts[1], 10);
    const seconds = parseInt(timeParts[2], 10);

    if (isNaN(hours) || hours < 0 || hours > 23) // special case - isNaN checks for invalid input, e.g., in a date format like 'NOV2504:00:03', where 'NOV2504' is not a valid hour.
      return `Error. invalid hours: ${hours} at row ${rowInd + 1}`;

    if (minutes < 0 || minutes > 59)
      return `Error. invalid minutes: ${minutes} at row ${rowInd + 1}`;

    if (seconds < 0 || seconds > 59)
      return `Error. invalid seconds: ${seconds} at row ${rowInd + 1}`;

    return null;
  }
}