import { Component, OnInit, Input } from '@angular/core';
import { SqNgCommonUtilsTime } from './../../../../sq-ng-common/src/lib/sq-ng-common.utils_time';   // direct reference, instead of via 'public-api.ts' as an Angular library. No need for 'ng build sq-ng-common'. see https://angular.io/guide/creating-libraries
import { gDiag, minDate } from './../../sq-globals';

// The MarketHealth table frame is shown immediately (without numbers) even at DOMContentLoaded time. And later, it is filled with data as it arrives.
// This avoid UI blinking at load and later shifting HTML elements under the table downwards.
// The main reason is that is how a sound UI logic should be assuming slow data channel. In the long term many data source will give data much-much later, 2-3 second later.
// We have to show the UI with empty cells. Window.loaded: 70ms, WebSocket data: 140ms, so it is worth doing it, as the data arrives 70ms later than window is ready.

type Nullable<T> = T | null;

class RtMktSumRtStat {
  public assetId = NaN;
  public last  = NaN;
  public lastUtc = '';
}

class RtMktSumNonRtStat {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public sqTicker = '';
  public ticker = '';
  public periodStartDate = ''; // preferred to be a new Date(), but when it arrives from server it is a string '2010-09-29T00:00:00' which is ET time zone and better to keep that way than converting to local time-zone Date object
  public periodEndDate = '';
  public periodStart = NaN;
  public periodEnd = NaN;   // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
  public periodHigh = NaN;
  public periodLow = NaN;
  public periodMaxDD = NaN;
  public periodMaxDU = NaN;
}

class UiTableColumn {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public sqTicker = '';
  public ticker = '';
  public isNavColumn = false;

  // NonRt stats directly from server
  public periodStartDate = ''; // preferred to be a new Date(), but when it arrives from server it is a string '2010-09-29T00:00:00' which is ET time zone and better to keep that way than converting to local time-zone Date object
  public periodEndDate = '';
  public periodStart = NaN;
  public periodEnd = NaN;
  public periodHigh = NaN;
  public periodLow = NaN;
  public periodMaxDD = NaN;
  public periodMaxDU = NaN;

  // Rt stats directly from server
  public last  = NaN; // last means real-time usually. If there is no real-time price, then the last known.
  public lastUtc = ''; 

  // calculated fields as numbers
  public periodReturn = NaN; // for period: from startDate to endDate
  public periodMaxDrawDown = NaN; // for period: from startDate to endDate
  public rtReturn = NaN;  // comparing last (rt) price to periodEnd price.
  public return = NaN;  // Total return (from startDate to endDate to last realtime): adding period-return and realtime-return together. Every other performance number (cagr, maxDD) is also Total.
  public cagr = NaN;
  public drawDown = NaN;
  public drawUp = NaN;
  public maxDrawDown = NaN;
  public maxDrawUp = NaN;

  // Ui required fields as strings
  public referenceUrl = '';

  // fields for the first row
  public rtReturnStr = '';
  public rtReturnSign = 1;
  public rtReturnClass = '';
  public rtTooltipStr1 = '';
  public rtTooltipStr2 = '';

  // fields for the second row
  public periodReturnStr = '';
  public periodMaxDrawDownStr = '';
  public returnStr = '';
  public cagrStr = '';
  public drawDownStr = '';
  public drawUpStr = '';
  public maxDrawDownStr = '';
  public maxDrawUpStr = '';

  public selectedPerfIndSign = 1; // return or cagr or maxDD
  public selectedPerfIndClass = ''; // positive or negative
  public selectedPerfIndStr = ''; // the value of the cell in the table
  public mainTooltipStr1 = '';
  public mainTooltipStr2 = '';
  public mainTooltipStr3 = '';
  public lookbackErrorStr = '';
  public lookbackErrorClass = '';

  constructor(sqTckr: string, tckr: string, isNavCol: boolean) {
    this.sqTicker = sqTckr;
    this.ticker = tckr;
    this.isNavColumn = isNavCol;
    if (!isNavCol) {
      this.referenceUrl = 'https://uk.tradingview.com/chart/?symbol=' + tckr;
    }
  }
}

class TradingHoursTimer {
  el: Element;
  constructor(element) {
    this.el = element;
    this.run();
    setInterval(() => this.run(), 60000);
  }

  run() {
    const todayET = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
    const hours = todayET.getHours();
    const minutes = todayET.getMinutes();
    let hoursSt = hours.toString();
    let minutesSt = minutes.toString();
    const dayOfWeek = todayET.getDay();
    const timeOfDay = hours * 60 + minutes;
    // in ET time:
    // Pre-market starts: 4:00 - 240 min
    // Regular trading starts: 09:30 - 570 min
    // Regular trading ends: 16:00 - 960 min
    // Post market ends: 20:00 - 1200 min
    let isOpenStr = '';
    if (dayOfWeek === 0 || dayOfWeek === 6) {
      isOpenStr = 'Today is weekend. U.S. market is closed.';
    } else if (timeOfDay < 4 * 60) {
      isOpenStr = 'Market is closed. Pre-market starts in ' + Math.floor((240 - timeOfDay) / 60) + 'h' + (240 - timeOfDay) % 60 + 'min.';
    } else if (timeOfDay < 9 * 60 + 30) {
      isOpenStr = 'Pre-market is open. Regular trading starts in ' + Math.floor((570 - timeOfDay) / 60) + 'h' + (570 - timeOfDay) % 60 + 'min.';
    } else if (timeOfDay < 16 * 60) {
      isOpenStr = 'Market is open. Market closes in ' + Math.floor((960 - timeOfDay) / 60) + 'h' + (960 - timeOfDay) % 60 + 'min.';
    } else if (timeOfDay < 20 * 60) {
      isOpenStr = 'Regular trading is closed. Post-market ends in ' + Math.floor((1200 - timeOfDay) / 60) + 'h' + (1200 - timeOfDay) % 60 + 'min.';
    } else {
      isOpenStr = 'U.S. market is closed.';
    }

    if (hoursSt.length < 2) {
      hoursSt = '0' + hoursSt;
    }
    if (minutesSt.length < 2) {
      minutesSt = '0' + minutesSt;
    }

    const clockStr = hoursSt + ':' + minutesSt + ' ET' + '<br>' + isOpenStr;
    this.el.innerHTML = clockStr;
  }
}


@Component({
  selector: 'app-market-health',
  templateUrl: './market-health.component.html',
  styleUrls: ['./market-health.component.scss']
})
export class MarketHealthComponent implements OnInit {

  @Input() _parentWsConnection?: WebSocket = undefined;    // this property will be input from above parent container
  nRtStatArrived = 0;
  nNonRtStatArrived = 0;
  lastRtMsgStr = 'Rt data from server';
  lastNonRtMsgStr = 'NonRt data from server';
  lastRtMsg: Nullable<RtMktSumRtStat[]> = null;
  lastNonRtMsg: Nullable<RtMktSumNonRtStat[]> = null;

  uiTableColumns: UiTableColumn[] = []; // this is connected to Angular UI with *ngFor. If pointer is replaced or if size changes, Angular should rebuild the DOM tree. UI can blink. To avoid that only modify its inner field strings.

  lookbackStartET: Date; // set in ctor. We need this in JS client to check that the received data is long enough or not (Expected Date)
  lookbackStartETstr: string; // set in ctor; We need this for sending String instruction to Server. Anyway, a  HTML <input date> is always a 	A DOMString representing a date in YYYY-MM-DD format, or empty. https://developer.mozilla.org/en-US/docs/Web/HTML/Element/input/date
  lookbackEndET: Date;
  lookbackEndETstr: string;

  static updateUi(lastRt: Nullable<RtMktSumRtStat[]>, lastNonRt: Nullable<RtMktSumNonRtStat[]>, lookbackStartDateET: Date, uiColumns: UiTableColumn[]) {
    // check if both array exist; instead of the old-school way, do ES5+ way: https://stackoverflow.com/questions/11743392/check-if-an-array-is-empty-or-exists
    if (!(Array.isArray(lastRt) && lastRt.length > 0 && Array.isArray(lastNonRt) && lastNonRt.length > 0)) {
      return;
    }

    // purge out uiCol.last, because user can change between NAVs, and it can happen that the Nav column had a real-time last price for one broker, but not after changing the broker. That case last price is not sent for that asset.
    for (const uiCol of uiColumns) {
      uiCol.last = NaN;
      uiCol.lastUtc = '';
    }

    // we have to prepare if the server sends another ticker that is not expected. In that rare case, the append it to the end of the array. That will cause UI blink. And Warn about it, so this can be fixed.
    // start with the NonRt, because that gives the AssetID to ticker definitions.
    for (const stockNonRt of lastNonRt) {
      let uiCol: UiTableColumn;
      const existingUiCols = uiColumns.filter(col => col.ticker === stockNonRt.ticker);
      if (existingUiCols.length === 0) {
        console.warn(`Received ticker '${stockNonRt.ticker}' is not expected. UiArray should be increased. This will cause UI redraw and blink. Add this ticker to defaultTickerExpected!`, 'background: #222; color: red');
        uiCol = new UiTableColumn(stockNonRt.sqTicker, stockNonRt.ticker, false);
        uiColumns.push(uiCol);
      } else if (existingUiCols.length === 1) {
        uiCol = existingUiCols[0];
      } else {
        console.warn(`Received ticker '${stockNonRt.ticker}' has duplicates in UiArray. This might be legit if both VOD.L and VOD wants to be used. ToDo: Differentiation based on assetId is needed.`, 'background: #222; color: red');
        uiCol = existingUiCols[0];
      }

      uiCol.assetId = stockNonRt.assetId;
      uiCol.periodStartDate = stockNonRt.periodStartDate;
      uiCol.periodEndDate = stockNonRt.periodEndDate;
      uiCol.periodStart = stockNonRt.periodStart;
      uiCol.periodEnd = stockNonRt.periodEnd;
      uiCol.periodHigh = stockNonRt.periodHigh;
      uiCol.periodLow = stockNonRt.periodLow;
      uiCol.periodMaxDD = stockNonRt.periodMaxDD;
      uiCol.periodMaxDU = stockNonRt.periodMaxDU;
    }

    for (const stockRt of lastRt) {
      const existingUiCols = uiColumns.filter(col => col.assetId === stockRt.assetId);
      if (existingUiCols.length === 0) {
        // it can easily happen. User changes the BrNav, but the realtime price of the previously selected BrNav is already in the websocket so it is coming here.
        console.warn(`Received assetId '${stockRt.assetId}' is not found in UiArray. Happens if user changes BrNav and old one is already in the way`, 'background: #222; color: red');
        break;
      }
      const uiCol = existingUiCols[0];
      uiCol.last = stockRt.last;
      uiCol.lastUtc = stockRt.lastUtc;
    }

    const indicatorSelected = (document.getElementById('perfIndicator') as HTMLSelectElement).value;
    const todayET = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
    todayET.setHours(0, 0, 0, 0); // get rid of the hours, minutes, seconds and milliseconds
    const nf = new Intl.NumberFormat();  // for thousands commas(,) https://stackoverflow.com/questions/2901102/how-to-print-a-number-with-commas-as-thousands-separators-in-javascript

    for (const uiCol of uiColumns) {
      // preparing values
      uiCol.periodReturn = uiCol.periodEnd / uiCol.periodStart - 1;
      uiCol.periodMaxDrawDown = uiCol.periodMaxDD;
      uiCol.rtReturn = uiCol.last > 0 ? uiCol.last / uiCol.periodEnd - 1 : 0;
      uiCol.return = uiCol.last > 0 ? uiCol.last / uiCol.periodStart - 1 : uiCol.periodEnd / uiCol.periodStart - 1;
      const dataStartDateET = new Date(uiCol.periodStartDate);  // '2010-09-29T00:00:00' which was UTC is converted to DateObj interpreted in Local time zone {Tue Sept 29 2010 00:00:00 GMT+0000 (Greenwich Mean Time)}
      const nDays = SqNgCommonUtilsTime.DateDiffNdays(dataStartDateET, todayET); // 2 weeks = 14 days, 2020 year: 366 days, because it is a leap year.
      const nYears = nDays / 365.25; // exact number of days in a year in average 365.25 days, because it is 3 times 365 and 1 time 366
      uiCol.cagr = Math.pow(1 + uiCol.return, 1.0 / nYears) - 1;
      uiCol.drawDown = uiCol.last > 0 ? uiCol.last / Math.max(uiCol.periodHigh, uiCol.last) - 1 : uiCol.periodEnd / uiCol.periodHigh - 1;
      uiCol.drawUp = uiCol.last > 0 ? uiCol.last / Math.min(uiCol.periodLow, uiCol.last) - 1 : uiCol.periodEnd / uiCol.periodLow - 1;
      uiCol.maxDrawDown = Math.min(uiCol.periodMaxDD, uiCol.drawDown);
      uiCol.maxDrawUp = Math.max(uiCol.periodMaxDU, uiCol.drawUp);

      // filling first row in table
      uiCol.rtReturnStr = (uiCol.rtReturn >= 0 ? '+' : '') + (uiCol.rtReturn * 100).toFixed(2).toString() + '%';
      uiCol.rtReturnSign = Math.sign(uiCol.rtReturn);
      uiCol.rtReturnClass = (uiCol.rtReturn >= 0 ? 'positivePerf' : 'negativePerf');
      uiCol.rtTooltipStr1 = uiCol.ticker;
      uiCol.rtTooltipStr2 = 'Period end value: ' + nf.format(uiCol.periodEnd) + '\n'  + 'Last value: ' + nf.format(uiCol.last) + ' (at ' + uiCol.lastUtc + ')\n' + 'Rt return: ' + (uiCol.rtReturn >= 0 ? '+' : '') + (uiCol.rtReturn * 100).toFixed(2).toString() + '%';
      //uiCol.rtTooltipStr2 = 'Period end price: ' + nf.format(uiCol.periodEnd) + '\n'  + 'Last price: ' + uiCol.last.toFixed(2).toString() + '\n' + 'Rt return: ' + (uiCol.rtReturn >= 0 ? '+' : '') + (uiCol.rtReturn * 100).toFixed(2).toString() + '%';

      // filling second row in table. Tooltip contains all indicators (return, DD, DU, maxDD, maxDU), so we have to compute them
      // const dataStartDate: Date = new Date(uiCol.periodStart);
      // https://stackoverflow.com/questions/17545708/parse-date-without-timezone-javascript
      // Javascript Date object are timestamps - they merely contain a number of milliseconds since the epoch. There is no timezone info in a Date object
      // uiCol.periodStart = '2010-09-29T00:00:00' comes as a string
      // dataStartDate.toISOString() would convert { Tue Dec 31 2019 00:00:00 GMT+0000 (GMT) } to "2015-09-28T23:00:00.000Z", so we better use the received string from server.
      const dataStartDateETStr = uiCol.periodStartDate.slice(0, 10); // use what is coming from server '2010-09-29T00:00:00'
      const dataEndDateETStr = uiCol.periodEndDate.slice(0, 10); // use what is coming from server '2010-09-29T00:00:00'
      uiCol.periodReturnStr = (uiCol.periodReturn >= 0 ? '+' : '') + (uiCol.periodReturn * 100).toFixed(2).toString() + '%';
      uiCol.periodMaxDrawDownStr = (uiCol.periodMaxDrawDown >= 0 ? '+' : '') + (uiCol.periodMaxDrawDown * 100).toFixed(2).toString() + '%';
      uiCol.returnStr = (uiCol.return >= 0 ? '+' : '') + (uiCol.return * 100).toFixed(2).toString() + '%';
      uiCol.cagrStr = (uiCol.cagr >= 0 ? '+' : '') + (uiCol.cagr * 100).toFixed(2).toString() + '%';
      uiCol.drawDownStr = (uiCol.drawDown >= 0 ? '+' : '') + (uiCol.drawDown * 100).toFixed(2).toString() + '%';
      uiCol.drawUpStr = (uiCol.drawUp >= 0 ? '+' : '') + (uiCol.drawUp * 100).toFixed(2).toString() + '%';
      uiCol.maxDrawDownStr = (uiCol.maxDrawDown >= 0 ? '+' : '') + (uiCol.maxDrawDown * 100).toFixed(2).toString() + '%';
      uiCol.maxDrawUpStr = (uiCol.maxDrawUp >= 0 ? '+' : '') + (uiCol.maxDrawUp * 100).toFixed(2).toString() + '%';

      uiCol.mainTooltipStr1 = uiCol.ticker;
      uiCol.mainTooltipStr2 = `Period only return: ${uiCol.periodReturnStr}\nPeriod only maxDD: ${uiCol.periodMaxDrawDownStr}\n\nPeriod+Rt return: ${uiCol.returnStr}\nTotal CAGR: ${uiCol.cagrStr}\nCurrent drawdown: ${uiCol.drawDownStr}\nCurrent drawup: ${uiCol.drawUpStr}\nMaximum drawdown: ${uiCol.maxDrawDownStr}\nMaximum drawup: ${uiCol.maxDrawUpStr}`;
      uiCol.mainTooltipStr3 = `\nPeriod [${dataStartDateETStr}...${dataEndDateETStr}]:\nStart: ${nf.format(uiCol.periodStart)}\nEnd: ${nf.format(uiCol.periodEnd)}\nHigh: ${nf.format(uiCol.periodHigh)}\nLow: ${nf.format(uiCol.periodLow)}\n*All returns are TWR, coz of adjustments.`;
      uiCol.lookbackErrorStr = (dataStartDateET > lookbackStartDateET) ? `! Period data starts on ${dataStartDateETStr}\n instead of the expected ${lookbackStartDateET.toISOString().slice(0, 10)}.\n\n` : '';
      uiCol.lookbackErrorClass = (dataStartDateET > lookbackStartDateET) ? 'lookbackError' : '';

      MarketHealthComponent.updateUiColumnBasedOnSelectedIndicator(uiCol, indicatorSelected);
     } // for
  }

  static updateUiColumnBasedOnSelectedIndicator(uiCol: UiTableColumn, indicatorSelected: string) {
    switch (indicatorSelected) {
      case 'TotRet':
        uiCol.selectedPerfIndSign = Math.sign(uiCol.return);
        uiCol.selectedPerfIndClass = (uiCol.selectedPerfIndSign === 1) ? 'positivePerf' : 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.returnStr;
        break;
      case 'TotCagr':
        uiCol.selectedPerfIndSign = Math.sign(uiCol.cagr);
        uiCol.selectedPerfIndClass = (uiCol.selectedPerfIndSign === 1) ? 'positivePerf' : 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.cagrStr;
        break;
      case 'CDD':
        uiCol.selectedPerfIndSign = -1;
        uiCol.selectedPerfIndClass = 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.drawDownStr;
        break;
      case 'CDU':
        uiCol.selectedPerfIndSign = 1;
        uiCol.selectedPerfIndClass = 'positivePerf';
        uiCol.selectedPerfIndStr = uiCol.drawUpStr;
        break;
      case 'MDD':
        uiCol.selectedPerfIndSign = -1;
        uiCol.selectedPerfIndClass = 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.maxDrawDownStr;
        break;
      case 'MDU':
        uiCol.selectedPerfIndSign = 1;
        uiCol.selectedPerfIndClass = 'positivePerf';
        uiCol.selectedPerfIndStr = uiCol.maxDrawUpStr;
        break;
      case 'PerRet':
        uiCol.selectedPerfIndSign = Math.sign(uiCol.periodReturn);
        uiCol.selectedPerfIndClass = (uiCol.selectedPerfIndSign === 1) ? 'positivePerf' : 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.periodReturnStr;
        break;
      case 'PerMDD':
        uiCol.selectedPerfIndSign = Math.sign(uiCol.periodMaxDrawDown);
        uiCol.selectedPerfIndClass = 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.periodMaxDrawDownStr;
        break;
      default:
        uiCol.selectedPerfIndSign = Math.sign(uiCol.return);
        uiCol.selectedPerfIndClass = '';
        uiCol.selectedPerfIndStr = '';
        break;
    } // switch
  }

  constructor() {
    const todayET = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
    todayET.setHours(0, 0, 0, 0); // get rid of the hours, minutes, seconds and milliseconds

    this.lookbackStartET = new Date(todayET.getFullYear() - 1, 11, 31);  // set YTD as default
    this.lookbackStartETstr = this.Date2PaddedIsoStr(this.lookbackStartET);

    // https://stackoverflow.com/questions/563406/add-days-to-javascript-date
    const yesterDayET = new Date(todayET);
    yesterDayET.setDate(yesterDayET.getDate() - 1);
    this.lookbackEndET = new Date(yesterDayET.getFullYear(), yesterDayET.getMonth(), yesterDayET.getDate());  // set yesterdayET as default
    this.lookbackEndETstr = this.Date2PaddedIsoStr(this.lookbackEndET);
    this.FillDataWithEmptyValuesToAvoidUiBlinkingWhenDataArrives();
  }

  // without this, table is not visualized initially and page blinks when it becomes visible 500ms after window loads.
  FillDataWithEmptyValuesToAvoidUiBlinkingWhenDataArrives(): void {
    // the header cells better to be fixed strings determined at window.load(), so UI doesn't blink when data arrives 100-500ms later. Therefore a general "BrNav" virtual ticker is fine.
    const defaultTickerExpected = ['BrNAV', 'QQQ', 'SPY', 'GLD', 'TLT', 'VXX', 'UNG', 'USO']; // to avoid UI blinking while building the UI early, we better know the number of columns, but then the name of tickers as well. If server sends something different, we will readjust with blinking UI.
    for (const tkr of defaultTickerExpected) {
      let sqTicker = tkr === 'BrNAV' ? tkr : "S/" + tkr;
      this.uiTableColumns.push(new UiTableColumn(sqTicker, tkr, tkr === 'BrNAV'));
    }
  }

  ngOnInit(): void {
         // tslint:disable-next-line: no-unused-expression
         new TradingHoursTimer(document.getElementById('tradingHoursTimer'));

  } // ngOnInit()

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'MktHlth.RtStat':  // this is the most frequent case. Should come first.
        if (gDiag.wsOnFirstRtMktSumRtStatTime === minDate) {
          gDiag.wsOnFirstRtMktSumRtStatTime = new Date();
        }
        gDiag.wsOnLastRtMktSumRtStatTime = new Date();
        gDiag.wsNumRtMktSumRtStat++;

        this.nRtStatArrived++;
        const jsonArrayObjRt = JSON.parse(msgObjStr);
        // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
        jsonArrayObjRt.forEach(element => {
          element.last = this.ChangeNaNstringToNaNnumber(element.last);
        });
        const msgStrRt = jsonArrayObjRt.map(s => s.assetId + '=>' + s.last.toFixed(2).toString()).join(', ');  // %Chg: Bloomberg, MarketWatch, TradingView doesn't put "+" sign if it is positive, IB, CNBC, YahooFinance does. Go as IB.
        console.log('ws: RtMktSumRtStat arrived: ' + msgStrRt);
        this.lastRtMsgStr = msgStrRt;
        this.lastRtMsg = jsonArrayObjRt;
        MarketHealthComponent.updateUi(this.lastRtMsg, this.lastNonRtMsg, this.lookbackStartET, this.uiTableColumns);
        return true;
      case 'MktHlth.NonRtStat':
        if (gDiag.wsOnFirstRtMktSumNonRtStatTime === minDate) {
          gDiag.wsOnFirstRtMktSumNonRtStatTime = new Date();
        }
        gDiag.wsOnLastRtMktSumNonRtStatTime = new Date();
        this.nNonRtStatArrived++;
        const jsonArrayObjNonRt = JSON.parse(msgObjStr);
        // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
        jsonArrayObjNonRt.forEach(element => {
          if (element.sqTicker.startsWith("S/"))
            element.ticker = element.sqTicker.substring(2); // "sqTicker":"S/QQQ"
          else
            element.ticker = element.sqTicker;  // "sqTicker":"BrNAV"
          element.periodStart = this.ChangeNaNstringToNaNnumber(element.periodStart);
          element.periodEnd = this.ChangeNaNstringToNaNnumber(element.periodEnd);
          element.periodHigh = this.ChangeNaNstringToNaNnumber(element.periodHigh);
          element.periodLow = this.ChangeNaNstringToNaNnumber(element.periodLow);
          element.periodMaxDD = this.ChangeNaNstringToNaNnumber(element.periodMaxDD);
          element.periodMaxDU = this.ChangeNaNstringToNaNnumber(element.periodMaxDU);
        });
        const msgStrNonRt = jsonArrayObjNonRt.map(s => s.assetId + '|' + s.ticker + '|periodEnd:' + s.periodEnd.toFixed(2).toString() + '|periodStart:' + s.periodStart.toString() + '|open:' + s.periodStart.toFixed(2).toString() + '|high:' + s.periodHigh.toFixed(2).toString() + '|low:' + s.periodLow.toFixed(2).toString() + '|mdd:' + s.periodMaxDD.toFixed(2).toString() + '|mdu:' + s.periodMaxDU.toFixed(2).toString()).join(', ');
        // console.log('ws: RtMktSumNonRtStat arrived: ' + msgStrNonRt);
        this.lastNonRtMsgStr = msgStrNonRt;
        this.lastNonRtMsg = jsonArrayObjNonRt;
        MarketHealthComponent.updateUi(this.lastRtMsg, this.lastNonRtMsg, this.lookbackStartET, this.uiTableColumns);
        return true;
      case 'MktHlth.Handshake':  // this is the least frequent case. Should come last.
        const jsonObjHandshake = JSON.parse(msgObjStr);
        console.log(`MktHlth.Handshake.Selectable NAVs: '${jsonObjHandshake.selectableNavs}'`);
        this.updateUiSelectableNavs(jsonObjHandshake.selectableNavs);
        return true;
      default:
        return false;
    }
  }

  private ChangeNaNstringToNaNnumber(elementField: any): number {
    if (elementField.toString() === 'NaN') {
      return NaN;
    } else {
      return Number(elementField);
    }
  }

  // https://stackoverflow.com/questions/44840735/change-vs-ngmodelchange-in-angular
  // "In angular 10 both change and ngModelChange appear to fire before the value is updated in the UI HTML element."
  onLookbackDateChange(pEvent: any) {  // "A DOMString representing a date in YYYY-MM-DD format, or empty" https://developer.mozilla.org/en-US/docs/Web/HTML/Element/input/date
    console.log(`Sq.onLookbackDateChange('${pEvent.target.id}'): from current '${this.lookbackStartETstr}'/'${this.lookbackEndETstr}' to '${pEvent.target.value}'`);
    // when user is typing "2020" it starts to arrive at every keystroke. After the first keystroke: "0002-10-27". So, check if it is a valid Date in the last 40 years. If it is, then ask update immediately.
    // 1. First check if it is a valid date or not.
    let isGoodDate = !(pEvent.target.value === '');  // https://stackoverflow.com/questions/154059/how-can-i-check-for-an-empty-undefined-null-string-in-javascript
    const parts = pEvent.target.value.split('-');
    const year = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10);
    const day = parseInt(parts[2], 10);
    if (year < 1980 || year > 2040) {
      isGoodDate = false;
    }
    if (month < 1 || month > 12) {
      isGoodDate = false;
    }
    if (day < 1 || day > 31) {
      isGoodDate = false;
    }
    if (!isGoodDate) {
      return;
    }

    (document.getElementById('lookBackPeriod') as HTMLSelectElement).value = 'Date';

    if (pEvent.target.id === 'startDate') {
      this.lookbackStartETstr = pEvent.target.value;
      this.lookbackStartET = this.PaddedIsoStr3Date(this.lookbackStartETstr);
    } else if (pEvent.target.id === 'endDate') {
      this.lookbackEndETstr = pEvent.target.value;
      this.lookbackEndET = this.PaddedIsoStr3Date(this.lookbackEndETstr);
    }
    this.onLookbackChange();
  }

  onLookbackSelectChange() {
    const lookbackStr = (document.getElementById('lookBackPeriod') as HTMLSelectElement).value;
    console.log('Sq.onClickChangeLookback(): ' + lookbackStr);
    const currDateET: Date = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
    if (lookbackStr === 'YTD') {
      this.lookbackStartET = new Date(currDateET.getFullYear() - 1, 11, 31);
    } else if (lookbackStr.endsWith('y')) {
      const lbYears = parseInt(lookbackStr.substr(0, lookbackStr.length - 1), 10);
      this.lookbackStartET = new Date(currDateET.setFullYear(currDateET.getFullYear() - lbYears));
    } else if (lookbackStr.endsWith('m')) {
      const lbMonths = parseInt(lookbackStr.substr(0, lookbackStr.length - 1), 10);
      this.lookbackStartET = new Date(currDateET.setMonth(currDateET.getMonth() - lbMonths));
    } else if (lookbackStr.endsWith('w')) {
      const lbWeeks = parseInt(lookbackStr.substr(0, lookbackStr.length - 1), 10);
      this.lookbackStartET = new Date(currDateET.setDate(currDateET.getDate() - lbWeeks * 7));
    } else if (lookbackStr === 'D\'99') {
      this.lookbackStartET = new Date(1999, 3 - 1, 10); // start date of QQQ
    } else if (lookbackStr === 'Date') {
      this.lookbackStartET = this.PaddedIsoStr3Date(this.lookbackStartETstr);
    }
    this.lookbackStartETstr = this.Date2PaddedIsoStr(this.lookbackStartET);

    if (!(lookbackStr === 'Date')) {  // change back the end date to yesterday, except if it is in CustomDate mode
      const todayET = SqNgCommonUtilsTime.ConvertDateLocToEt(new Date());
      todayET.setHours(0, 0, 0, 0); // get rid of the hours, minutes, seconds and milliseconds
      const yesterDayET = new Date(todayET);
      yesterDayET.setDate(yesterDayET.getDate() - 1);
      this.lookbackEndET = new Date(yesterDayET.getFullYear(), yesterDayET.getMonth(), yesterDayET.getDate());  // set yesterdayET as default
      this.lookbackEndETstr = this.Date2PaddedIsoStr(this.lookbackEndET);
    }
    this.onLookbackChange();
  }

  onLookbackChange() {
    console.log('Calling server with new lookback. StartDateETstr: ' + this.lookbackStartETstr + ', lookbackStartET: ' + this.lookbackStartET);
    gDiag.wsOnLastRtMktSumLookbackChgStart = new Date();
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN) {
      this._parentWsConnection.send('changeLookback:Date:' + this.lookbackStartETstr + '...' + this.lookbackEndETstr); // we always send the Date format to server, not the strings of 'YTD/10y'
    }
  }

  public perfIndicatorSelector(): void {
    const indicatorSelected = (document.getElementById('perfIndicator') as HTMLSelectElement).value;
    for (const uiCol of this.uiTableColumns) {
      MarketHealthComponent.updateUiColumnBasedOnSelectedIndicator(uiCol, indicatorSelected);
    }
  }

  updateUiSelectableNavs(pSelectableNavs: string) {
    const navSelectElement = document.getElementById('navSelect') as HTMLSelectElement;
    for (const nav of pSelectableNavs.split(',')) {
      navSelectElement.options[navSelectElement.options.length] = new Option(nav, nav);
    }
    navSelectElement.selectedIndex = 0; // select the first item
  }

  onNavHeaderClicked(pEvent: any) {
    // https://www.w3schools.com/howto/howto_js_popup.asp
    // When the user clicks on header, open the popup
    // https://stackoverflow.com/questions/10554446/no-onclick-when-child-is-clicked
    // part of the event object is the target member. This will tell you which element triggered the event to begin with.
    console.log('onNavHeaderClicked()');
    const popupSpan = document.getElementById('navHeaderPopupId') as HTMLSpanElement;
    if (!(pEvent.target === popupSpan)) { // if not child popup, but the header
      popupSpan.classList.toggle('show');
    }
  }

  onNavHeaderPopupClicked(pEvent: any) {
    console.log('onNavHeaderPopupClicked()');
    pEvent.stopPropagation();
  }

  onNavSelectChange(pEvent: any) {
    const navSelectTicker = (document.getElementById('navSelect') as HTMLSelectElement).value;
    console.log(navSelectTicker);
    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN) {
      this._parentWsConnection.send('changeNav:' + navSelectTicker);
    }
  }

  // zeroPad = (num, places: number) => String(num).padStart(places, '0');  // https://stackoverflow.com/questions/2998784/how-to-output-numbers-with-leading-zeros-in-javascript
  // ES5 approach: because 2021-02: it works in CLI, but VsCode shows problems: "Property 'padStart' does not exist on type 'string'. Do you need to change your target library? Try changing the `lib` compiler option to 'es2017' or later."
  public zeroPad(num, places) {
    var zero = places - num.toString().length + 1;
    return Array(+(zero > 0 && zero)).join("0") + num;
  }

  public Date2PaddedIsoStr(date: Date): string {  // 2020-9-1 is not acceptable. Should be converted to 2020-09-01
    // don't use UTC versions, because they will convert local time zone dates to UTC first, then we might have bad result.
    // "date = 'Tue Apr 13 2021 00:00:00 GMT+0100 (British Summer Time)'" because local BST is not UTC date.getUTCDate() = 12, while date.getDate()=13 (correct)
    //return this.zeroPad(date.getUTCFullYear(), 4) + '-' + this.zeroPad(date.getUTCMonth() + 1, 2) + '-' + this.zeroPad(date.getUTCDate(), 2);
    return this.zeroPad(date.getFullYear(), 4) + '-' + this.zeroPad(date.getMonth() + 1, 2) + '-' + this.zeroPad(date.getDate(), 2);
  }

  public PaddedIsoStr3Date(dateStr: string): Date {
    const parts = dateStr.split('-');
    const year = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10);
    const day = parseInt(parts[2], 10);
    return new Date(year, month - 1, day);
  }
}
