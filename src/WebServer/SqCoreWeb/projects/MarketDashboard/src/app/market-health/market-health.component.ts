import { Component, OnInit, Input } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
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
}

class RtMktSumNonRtStat {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public ticker = '';
  public previousClose = NaN;   // If SignalR receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
  public periodStart = new Date();
  public periodOpen = NaN;
  public periodHigh = NaN;
  public periodLow = NaN;
  public periodMaxDD = NaN;
  public periodMaxDU = NaN;
}

class UiTableColumn {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public ticker = '';

  // NonRt stats directly from server
  public previousClose = NaN;
  public periodStart = new Date();
  public periodOpen = NaN;
  public periodHigh = NaN;
  public periodLow = NaN;
  public periodMaxDD = NaN;
  public periodMaxDU = NaN;

  // Rt stats directly from server
  public last  = NaN;

  // calculated fields as numbers
  public dailyReturn = NaN;
  public periodReturn = NaN;
  public drawDownPct = NaN;
  public drawUpPct = NaN;
  public maxDrawDownPct = NaN;
  public maxDrawUpPct = NaN;

  // Ui required fields as strings
  public referenceUrl = '';

  // fields for the first row
  public dailyReturnStr = '';
  public dailyReturnSign = 1;
  public dailyReturnClass = '';
  public dailyTooltipStr1 = '';
  public dailyTooltipStr2 = '';

  // fields for the second row
  public periodReturnStr = '';
  public drawDownPctStr = '';
  public drawUpPctStr = '';
  public maxDrawDownPctStr = '';
  public maxDrawUpPctStr = '';
  public selectedPerfIndSign = 1;
  public selectedPerfIndClass = '';
  public selectedPerfIndStr = '';
  public periodPerfTooltipStr1 = '';
  public periodPerfTooltipStr2 = '';
  public periodPerfTooltipStr3 = '';
  public lookbackErrorString = '';
  public lookbackErrorClass = '';

  constructor(tckr: string) {
    this.ticker = tckr;
    this.referenceUrl = 'https://uk.tradingview.com/chart/?symbol=' + tckr;
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
    const time: Date = new Date();
    const time2 = SqNgCommonUtilsTime.ConvertDateLocToEt(time);
    const hours = time2.getHours();
    const minutes = time2.getMinutes();
    let hoursSt = hours.toString();
    let minutesSt = minutes.toString();
    const dayOfWeek = time2.getDay();
    const timeOfDay = hours * 60 + minutes;
    // in ET time:
    // Pre-market starts: 4:00 - 240 min
    // Regular trading starts: 09:30 - 570 min
    // Regular trading ends: 16:00 - 960 min
    // Post market ends: 20:00 - 1200 min
    let isOpenStr = '';
    if (dayOfWeek === 0 || dayOfWeek === 6) {
      isOpenStr = 'Today is weekend. U.S. market is closed.';
    } else if (timeOfDay < 240) {
      isOpenStr = 'Market is closed. Pre-market starts in ' + Math.floor((240 - timeOfDay) / 60) + 'h' + (240 - timeOfDay) % 60 + 'min.';
    } else if (timeOfDay < 570) {
      isOpenStr = 'Pre-market is open. Regular trading starts in ' + Math.floor((570 - timeOfDay) / 60) + 'h' + (570 - timeOfDay) % 60 + 'min.';
    } else if (timeOfDay < 960) {
      isOpenStr = 'Market is open. Market closes in ' + Math.floor((960 - timeOfDay) / 60) + 'h' + (960 - timeOfDay) % 60 + 'min.';
    } else if (timeOfDay < 1200) {
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

  @Input() _parentHubConnection?: HubConnection = undefined;    // although SignalR is not used, leave it for Diagnostics purposes. To compare speed to WebSocket. This property will be input from above parent container
  @Input() _parentWsConnection?: WebSocket = undefined;    // this property will be input from above parent container
  nRtStatArrived = 0;
  nNonRtStatArrived = 0;
  lastRtMsgStr = 'Rt data from server';
  lastNonRtMsgStr = 'NonRt data from server';
  lastRtMsg: Nullable<RtMktSumRtStat[]> = null;
  lastNonRtMsg: Nullable<RtMktSumNonRtStat[]> = null;

  uiTableColumns: UiTableColumn[] = []; // this is connected to Angular UI with *ngFor. If pointer is replaced or if size changes, Angular should rebuild the DOM tree. UI can blink. To avoid that only modify its inner field strings.

  currDateTime: Date = new Date(); // current date and time
  lookbackStart: Date = new Date(this.currDateTime.getUTCFullYear() - 1, 11, 31);  // set YTD as default

  static updateUi(lastRt: Nullable<RtMktSumRtStat[]>, lastNonRt: Nullable<RtMktSumNonRtStat[]>, lookbackStartDate: Date, uiColumns: UiTableColumn[]) {
    // check if both array exist; instead of the old-school way, do ES5+ way: https://stackoverflow.com/questions/11743392/check-if-an-array-is-empty-or-exists
    if (!(Array.isArray(lastRt) && lastRt.length > 0 && Array.isArray(lastNonRt) && lastNonRt.length > 0)) {
      return;
    }

    // we have to prepare if the server sends another ticker that is not expected. In that rare case, the append it to the end of the array. That will cause UI blink. And Warn about it, so this can be fixed.
    // start with the NonRt, because that gives the AssetID to ticker definitions.
    for (const stockNonRt of lastNonRt) {
      let uiCol: UiTableColumn;
      const existingUiCols = uiColumns.filter(col => col.ticker === stockNonRt.ticker);
      if (existingUiCols.length === 0) {
        console.warn(`Received ticker '${stockNonRt.ticker}' is not expected. UiArray should be increased. This will cause UI redraw and blink. Add this ticker to defaultTickerExpected!`, 'background: #222; color: red');
        uiCol = new UiTableColumn(stockNonRt.ticker);
        uiColumns.push(uiCol);
      } else if (existingUiCols.length === 1) {
        uiCol = existingUiCols[0];
      } else {
        console.warn(`Received ticker '${stockNonRt.ticker}' has duplicates in UiArray. This might be legit if both VOD.L and VOD wants to be used. ToDo: Differentiation based on assetId is needed.`, 'background: #222; color: red');
        uiCol = existingUiCols[0];
      }

      uiCol.assetId = stockNonRt.assetId;
      uiCol.previousClose = stockNonRt.previousClose;
      uiCol.periodStart = stockNonRt.periodStart;
      uiCol.periodOpen = stockNonRt.periodOpen;
      uiCol.periodHigh = stockNonRt.periodHigh;
      uiCol.periodLow = stockNonRt.periodLow;
      uiCol.periodMaxDD = stockNonRt.periodMaxDD;
      uiCol.periodMaxDU = stockNonRt.periodMaxDU;
    }

    for (const stockRt of lastRt) {
      const existingUiCols = uiColumns.filter(col => col.assetId === stockRt.assetId);
      if (existingUiCols.length === 0) {
        console.warn(`Received assetId '${stockRt.assetId}' is not found in UiArray.`, 'background: #222; color: red');
        break;
      }
      const uiCol = existingUiCols[0];
      uiCol.last = stockRt.last;
    }

    const indicatorSelected = (document.getElementById('marketIndicator') as HTMLSelectElement).value;

    for (const uiCol of uiColumns) {
      // preparing values
      uiCol.dailyReturn = uiCol.last > 0 ? uiCol.last / uiCol.previousClose - 1 : 0;
      uiCol.periodReturn = uiCol.last > 0 ? uiCol.last / uiCol.periodOpen - 1 : uiCol.previousClose / uiCol.periodOpen - 1;
      uiCol.drawDownPct = uiCol.last > 0 ? uiCol.last / Math.max(uiCol.periodHigh, uiCol.last) - 1 : uiCol.previousClose / uiCol.periodHigh - 1;
      uiCol.drawUpPct = uiCol.last > 0 ? uiCol.last / Math.min(uiCol.periodLow, uiCol.last) - 1 : uiCol.previousClose / uiCol.periodLow - 1;
      uiCol.maxDrawDownPct = Math.min(uiCol.periodMaxDD, uiCol.drawDownPct);
      uiCol.maxDrawUpPct = Math.max(uiCol.periodMaxDU, uiCol.drawUpPct);

      // filling first row in table
      uiCol.dailyReturnStr = (uiCol.dailyReturn >= 0 ? '+' : '') + (uiCol.dailyReturn * 100).toFixed(2).toString() + '%';
      uiCol.dailyReturnSign = Math.sign(uiCol.dailyReturn);
      uiCol.dailyReturnClass = (uiCol.dailyReturn >= 0 ? 'positivePerf' : 'negativePerf');
      uiCol.dailyTooltipStr1 = uiCol.ticker;
      uiCol.dailyTooltipStr2 = 'Daily return: ' + (uiCol.dailyReturn >= 0 ? '+' : '') + (uiCol.dailyReturn * 100).toFixed(2).toString() + '%' + '\n' + 'Last price: ' + uiCol.last.toFixed(2).toString() + '\n' + 'Previous close price: ' + uiCol.previousClose.toFixed(2).toString();

      // filling second row in table. Tooltip contains all indicators (return, DD, DU, maxDD, maxDU), so we have to compute them
      const dataStartDate: Date = new Date(uiCol.periodStart);
      uiCol.periodReturnStr = (uiCol.periodReturn >= 0 ? '+' : '') + (uiCol.periodReturn * 100).toFixed(2).toString() + '%';
      uiCol.drawDownPctStr = (uiCol.drawDownPct >= 0 ? '+' : '') + (uiCol.drawDownPct * 100).toFixed(2).toString() + '%';
      uiCol.drawUpPctStr = (uiCol.drawUpPct >= 0 ? '+' : '') + (uiCol.drawUpPct * 100).toFixed(2).toString() + '%';
      uiCol.maxDrawDownPctStr = (uiCol.maxDrawDownPct >= 0 ? '+' : '') + (uiCol.maxDrawDownPct * 100).toFixed(2).toString() + '%';
      uiCol.maxDrawUpPctStr = (uiCol.maxDrawUpPct >= 0 ? '+' : '') + (uiCol.maxDrawUpPct * 100).toFixed(2).toString() + '%';

      uiCol.periodPerfTooltipStr1 = uiCol.ticker;
      uiCol.periodPerfTooltipStr2 = 'Period return: ' + uiCol.periodReturnStr + '\r\n' + 'Current drawdown: ' + uiCol.drawDownPctStr + '\r\n' + 'Current drawup: ' + uiCol.drawUpPctStr + '\r\n' + 'Maximum drawdown: ' + uiCol.maxDrawDownPctStr + '\r\n' + 'Maximum drawup: ' + uiCol.maxDrawUpPctStr;
      uiCol.periodPerfTooltipStr3 = '\r\n' + 'Period start: ' + dataStartDate.toISOString().slice(0, 10) + '\r\n' + 'Previous close: ' + uiCol.previousClose.toFixed(2).toString() + '\r\n' + 'Period open: ' + uiCol.periodOpen.toFixed(2).toString() + '\r\n' + 'Period high: ' + uiCol.periodHigh.toFixed(2).toString() + '\r\n' + 'Period low: ' + uiCol.periodLow.toFixed(2).toString();
      uiCol.lookbackErrorString = (dataStartDate > lookbackStartDate) ? ('! Period data starts on ' + dataStartDate.toISOString().slice(0, 10) + '\r\n' + ' instead of the expected ' + lookbackStartDate.toISOString().slice(0, 10)) + '. \r\n\r\n' : '';
      uiCol.lookbackErrorClass = (dataStartDate > lookbackStartDate) ? 'lookbackError' : '';

      MarketHealthComponent.updateUiColumnBasedOnSelectedIndicator(uiCol, indicatorSelected);
     } // for
  }

  static updateUiColumnBasedOnSelectedIndicator(uiCol: UiTableColumn, indicatorSelected: string) {
    switch (indicatorSelected) {
      case 'PerRet':
        uiCol.selectedPerfIndSign = Math.sign(uiCol.periodReturn);
        uiCol.selectedPerfIndClass = (uiCol.selectedPerfIndSign === 1) ? 'positivePerf' : 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.periodReturnStr;
        break;
      case 'CDD':
        uiCol.selectedPerfIndSign = -1;
        uiCol.selectedPerfIndClass = 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.drawDownPctStr;
        break;
      case 'CDU':
        uiCol.selectedPerfIndSign = 1;
        uiCol.selectedPerfIndClass = 'positivePerf';
        uiCol.selectedPerfIndStr = uiCol.drawUpPctStr;
        break;
      case 'MDD':
        uiCol.selectedPerfIndSign = -1;
        uiCol.selectedPerfIndClass = 'negativePerf';
        uiCol.selectedPerfIndStr = uiCol.maxDrawDownPctStr;
        break;
      case 'MDU':
        uiCol.selectedPerfIndSign = 1;
        uiCol.selectedPerfIndClass = 'positivePerf';
        uiCol.selectedPerfIndStr = uiCol.maxDrawUpPctStr;
        break;
      default:
        uiCol.selectedPerfIndSign = Math.sign(uiCol.periodReturn);
        uiCol.selectedPerfIndClass = '';
        uiCol.selectedPerfIndStr = '';
        break;
    } // switch
  }

  constructor() {
    this.FillDataWithEmptyValuesToAvoidUiBlinkingWhenDataArrives();
  }

  // without this, table is not visualized initially and page blinks when it becomes visible 500ms after window loads.
  FillDataWithEmptyValuesToAvoidUiBlinkingWhenDataArrives(): void {
    const defaultTickerExpected = ['QQQ', 'SPY', 'GLD', 'TLT', 'VXX', 'UNG', 'USO']; // to avoid UI blinking while building the UI early, we better know the number of columns, but then the name of tickers as well. If server sends something different, we will readjust with blinking UI.
    for (const tkr of defaultTickerExpected) {
      this.uiTableColumns.push(new UiTableColumn(tkr));
    }
  }

  ngOnInit(): void {
    if (this._parentHubConnection != null) {
      this._parentHubConnection.on('RtMktSumRtStat', (message: RtMktSumRtStat[]) => {
        if (gDiag.srOnFirstRtMktSumRtStatTime === minDate) {
          gDiag.srOnFirstRtMktSumRtStatTime = new Date();
          console.log('sq.d: ' + gDiag.srOnFirstRtMktSumRtStatTime.toISOString() + ': srOnFirstRtMktSumRtStatTime()'); // called 17ms after main.ts
        }
        gDiag.srOnLastRtMktSumRtStatTime = new Date();
        gDiag.srNumRtMktSumRtStat++;

        // native Websocket handles this now, not SignalR
        // this.nRtStatArrived++;
        // const msgStr = message.map(s => s.assetId + ' ? =>' + s.last.toFixed(2).toString()).join(', ');  // %Chg: Bloomberg, MarketWatch, TradingView doesn't put "+" sign if it is positive, IB, CNBC, YahooFinance does. Go as IB.
        // console.log('sr: RtMktSumRtStat arrived: ' + msgStr);
        // this.lastRtStatStr = msgStr;
        // this.updateMktSumRt(message, this.marketFullStat);
      });

      this._parentHubConnection.on('RtMktSumNonRtStat', (message: RtMktSumNonRtStat[]) => {
        if (gDiag.srOnFirstRtMktSumNonRtStatTime === minDate) {
          gDiag.srOnFirstRtMktSumNonRtStatTime = new Date();
          console.log('sq.d: ' + gDiag.srOnFirstRtMktSumNonRtStatTime.toISOString() + ': srOnFirstRtMktSumNonRtStatTime()'); // called 17ms after main.ts
        }

        // native Websocket handles this now, not SignalR
        // this.nNonRtStatArrived++;
        // // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
        // message.forEach(element => {
        //   if (element.previousClose.toString() === 'NaN') {
        //     element.previousClose = NaN;
        //   } else {
        //     element.previousClose = Number(element.previousClose);
        //   }
        // });
        // const msgStr = message.map(s => s.assetId + '-' + s.ticker + ':prevClose-' + s.previousClose.toFixed(2).toString() + ' : periodStart-' + s.periodStart.toString() + ':open-' + s.periodOpen.toFixed(2).toString() + '/high-' + s.periodHigh.toFixed(2).toString() + '/low-' + s.periodLow.toFixed(2).toString() + '/mdd' + s.periodMaxDD.toFixed(2).toString() + '/mdu' + s.periodMaxDU.toFixed(2).toString()).join(', ');
        // console.log('ws: RtMktSumNonRtStat arrived: ' + msgStr);
        // this.lastNonRtStatStr = msgStr;
        // this.updateMktSumNonRt(message, this.marketFullStat);
      });

     // tslint:disable-next-line: no-unused-expression
      new TradingHoursTimer(document.getElementById('tradingHoursTimer'));

    }
  } // ngOnInit()

  public webSocketOnMessage(msgCode: string, msgObjStr: string): boolean {
    switch (msgCode) {
      case 'RtMktSumRtStat':  // this is the most frequent case. Should come first.
        if (gDiag.wsOnFirstRtMktSumRtStatTime === minDate) {
          gDiag.wsOnFirstRtMktSumRtStatTime = new Date();
        }
        gDiag.wsOnLastRtMktSumRtStatTime = new Date();
        gDiag.wsNumRtMktSumRtStat++;

        this.nRtStatArrived++;
        const jsonArrayObjRt = JSON.parse(msgObjStr);
        const msgStrRt = jsonArrayObjRt.map(s => s.assetId + '=>' + s.last.toFixed(2).toString()).join(', ');  // %Chg: Bloomberg, MarketWatch, TradingView doesn't put "+" sign if it is positive, IB, CNBC, YahooFinance does. Go as IB.
        console.log('ws: RtMktSumRtStat arrived: ' + msgStrRt);
        this.lastRtMsgStr = msgStrRt;
        this.lastRtMsg = jsonArrayObjRt;
        MarketHealthComponent.updateUi(this.lastRtMsg, this.lastNonRtMsg, this.lookbackStart, this.uiTableColumns);
        return true;
      case 'RtMktSumNonRtStat':
        if (gDiag.wsOnFirstRtMktSumNonRtStatTime === minDate) {
          gDiag.wsOnFirstRtMktSumNonRtStatTime = new Date();
        }
        this.nNonRtStatArrived++;
        const jsonArrayObjNonRt = JSON.parse(msgObjStr);
        // If serializer receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
        jsonArrayObjNonRt.forEach(element => {
          if (element.previousClose.toString() === 'NaN') {
            element.previousClose = NaN;
          } else {
            element.previousClose = Number(element.previousClose);
          }
        });
        const msgStrNonRt = jsonArrayObjNonRt.map(s => s.assetId + '|' + s.ticker + '|prevClose:' + s.previousClose.toFixed(2).toString() + '|periodStart:' + s.periodStart.toString() + '|open:' + s.periodOpen.toFixed(2).toString() + '|high:' + s.periodHigh.toFixed(2).toString() + '|low:' + s.periodLow.toFixed(2).toString() + '|mdd:' + s.periodMaxDD.toFixed(2).toString() + '|mdu:' + s.periodMaxDU.toFixed(2).toString()).join(', ');
        console.log('ws: RtMktSumNonRtStat arrived: ' + msgStrNonRt);
        this.lastNonRtMsgStr = msgStrNonRt;
        this.lastNonRtMsg = jsonArrayObjNonRt;
        MarketHealthComponent.updateUi(this.lastRtMsg, this.lastNonRtMsg, this.lookbackStart, this.uiTableColumns);
        return true;
      default:
        return false;
    }
  }

  onClickChangeLookback() {
    const lookbackStr = (document.getElementById('lookBackPeriod') as HTMLSelectElement).value;
    console.log('Sq.onClickChangeLookback(): ' + lookbackStr);
    const currDate: Date = new Date();

    if (lookbackStr === 'YTD') {
      this.lookbackStart = new Date(currDate.getUTCFullYear() - 1, 11, 31);
    } else if (lookbackStr.endsWith('y')) {
      const lbYears = parseInt(lookbackStr.substr(0, lookbackStr.length - 1), 10);
      this.lookbackStart = new Date(currDate.setUTCFullYear(currDate.getUTCFullYear() - lbYears));
    } else if (lookbackStr.endsWith('m')) {
      const lbMonths = parseInt(lookbackStr.substr(0, lookbackStr.length - 1), 10);
      this.lookbackStart = new Date(currDate.setUTCMonth(currDate.getUTCMonth() - lbMonths));
    } else if (lookbackStr.endsWith('w')) {
      const lbWeeks = parseInt(lookbackStr.substr(0, lookbackStr.length - 1), 10);
      this.lookbackStart = new Date(currDate.setUTCDate(currDate.getUTCDate() - lbWeeks * 7));
    }

    console.log('New lookback startDate: ' + this.lookbackStart);

    if (this._parentWsConnection != null && this._parentWsConnection.readyState === WebSocket.OPEN) {
      this._parentWsConnection.send('changeLookback:' + lookbackStr);
    }
    // native Websocket handles this now, not SignalR
    // if (this._parentHubConnection != null) {
    //   this._parentHubConnection.invoke('changeLookback', lookbackStr)
    //     .then((message: RtMktSumNonRtStat[]) => {
    //       // If SignalR receives NaN string, it creates a "NaN" string here instead of NaN Number. Revert it immediately.
    //       message.forEach(element => {
    //         if (element.previousClose.toString() === 'NaN') {
    //           element.previousClose = NaN;
    //         } else {
    //           element.previousClose = Number(element.previousClose);
    //         }
    //       });
    //       this.updateMktSumNonRt(message, this.marketFullStat);
    //       const msgStr = message.map(s => s.assetId + '-' + s.ticker + ':prevClose-' + s.previousClose.toFixed(2).toString() + ' : periodStart-' + s.periodStart.toString() + ':open-' + s.periodOpen.toFixed(2).toString() + '/high-' + s.periodHigh.toFixed(2).toString() + '/low-' + s.periodLow.toFixed(2).toString() + '/mdd' + s.periodMaxDD.toFixed(2).toString() + '/mdu' + s.periodMaxDU.toFixed(2).toString()).join(', ');
    //       console.log('ws: onClickChangeLookback() got back message ' + msgStr);
    //       this.lastNonRtStatStr = msgStr;
    //     });
    // }
  }

  public perfIndicatorSelector(): void {
    const indicatorSelected = (document.getElementById('marketIndicator') as HTMLSelectElement).value;
    for (const uiCol of this.uiTableColumns) {
      MarketHealthComponent.updateUiColumnBasedOnSelectedIndicator(uiCol, indicatorSelected);
    }
  }

}
