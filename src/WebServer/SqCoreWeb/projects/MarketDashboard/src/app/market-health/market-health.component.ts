import { Component, OnInit, Input } from '@angular/core';
import { HubConnection } from '@microsoft/signalr';
import { SqNgCommonUtilsTime } from './../../../../sq-ng-common/src/lib/sq-ng-common.utils_time';   // direct reference, instead of via 'public-api.ts' as an Angular library. No need for 'ng build sq-ng-common'. see https://angular.io/guide/creating-libraries
import { gDiag, minDate } from './../../sq-globals';

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

class RtMktSumFullStat {
  public assetId = NaN;  // JavaScript Numbers are Always 64-bit Floating Point
  public ticker = '';
  public last  = NaN;
  public previousClose = NaN;
  public periodStart = new Date();
  public periodOpen = NaN;
  public periodHigh = NaN;
  public periodLow = NaN;
  public dailyReturn = NaN;
  public periodReturn = NaN;
  public drawDownPct = NaN;
  public drawUpPct = NaN;
  public maxDrawDownPct = NaN;
  public maxDrawUpPct = NaN;
}

class TableHeaderRefs {
  public ticker = '';
  public reference = '';
}

class DailyPerf {
  public ticker = '';
  public dailyReturnStr = '';
  public dailyReturnSign = 1;
  public dailyReturnClass = '';
  public dailyTooltipStr1 = '';
  public dailyTooltipStr2 = '';
}

class PeriodPerf {
  public ticker = '';
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
    // ET idoben:
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

  @Input() _parentHubConnection?: HubConnection = undefined;    // this property will be input from above parent container
  @Input() _parentWsConnection?: WebSocket = undefined;    // this property will be input from above parent container
  nRtStatArrived = 0;
  nNonRtStatArrived = 0;
  rtMktSumPrevCloseStr = 'A';
  rtMktSumRtQuoteStr = 'B';
  rtMktSumPeriodStatStr = 'C';
  rtMktSumToDashboard = 'Dashboard';
  marketFullStat: RtMktSumFullStat[] = [];
  tableHeaderLinks: TableHeaderRefs[] = [];
  perfIndDaily: DailyPerf[] = [];
  perfIndPeriodFull: PeriodPerf[] = [];
  perfIndPeriod: string[] = [];
  perfIndCDD: string[] = [];
  perfIndCDU: string[] = [];
  perfIndMDD: string[] = [];
  perfIndMDU: string[] = [];
  perfIndSelected: string[] = [];
  currDateN: Date = new Date();
  lookbackStart: Date = new Date(this.currDateN.getUTCFullYear() - 1, 11, 31);

  constructor() {
    this.FillDataWithEmptyValuesToAvoidUiBlinkingWhenDataArrives();
  }

  // without this, table is not visualized initially and page blinks when it becomes visible 500ms after window loads.
  FillDataWithEmptyValuesToAvoidUiBlinkingWhenDataArrives(): void {


    // makeUiTableVisible() consider using this, or better write new logic.
    // table should be visualized initially.
    // then when first data arrives, do nothing. Just store it in a virtual place. Not in the final place.
    // ONLY when BOTH data arrives do swap data members
  }

  public perfIndicatorSelector(): void {
    const value = (document.getElementById('marketIndicator') as HTMLSelectElement).value;
    switch (value) {
      case 'PerRet':
        for (const items of this.perfIndPeriodFull) {
          items.selectedPerfIndStr = items.periodReturnStr;
          items.selectedPerfIndClass = (items.selectedPerfIndSign === 1) ? 'positivePerf' : 'negativePerf';
        }
        break;
      case 'CDD':
        for (const items of this.perfIndPeriodFull) {
          items.selectedPerfIndSign = -1;
          items.selectedPerfIndClass = 'negativePerf';
          items.selectedPerfIndStr = items.drawDownPctStr;
        }
        break;
      case 'CDU':
        for (const items of this.perfIndPeriodFull) {
          items.selectedPerfIndSign = 1;
          items.selectedPerfIndClass = 'positivePerf';
          items.selectedPerfIndStr = items.drawUpPctStr;
        }
        break;
      case 'MDD':
        for (const items of this.perfIndPeriodFull) {
          items.selectedPerfIndSign = -1;
          items.selectedPerfIndClass = 'negativePerf';
          items.selectedPerfIndStr = items.maxDrawDownPctStr;
        }
        break;
      case 'MDU':
        for (const items of this.perfIndPeriodFull) {
          items.selectedPerfIndSign = 1;
          items.selectedPerfIndClass = 'positivePerf';
          items.selectedPerfIndStr = items.maxDrawUpPctStr;
        }
        break;
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
        // this.rtMktSumRtQuoteStr = msgStr;
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
        // this.rtMktSumPeriodStatStr = msgStr;
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
          // console.log('sq.d: ' + gDiag.wsOnFirstRtMktSumRtStatTime.toISOString() + ': wsOnFirstRtMktSumRtStatTime()'); // called 17ms after main.ts
        }
        gDiag.wsOnLastRtMktSumRtStatTime = new Date();
        gDiag.wsNumRtMktSumRtStat++;

        this.nRtStatArrived++;
        const jsonArrayObjRt = JSON.parse(msgObjStr);
        const msgStrRt = jsonArrayObjRt.map(s => s.assetId + ' ? =>' + s.last.toFixed(2).toString()).join(', ');  // %Chg: Bloomberg, MarketWatch, TradingView doesn't put "+" sign if it is positive, IB, CNBC, YahooFinance does. Go as IB.
        console.log('ws: RtMktSumRtStat arrived: ' + msgStrRt);
        this.rtMktSumRtQuoteStr = msgStrRt;

        this.updateMktSumRt(jsonArrayObjRt, this.marketFullStat);

        return true;
      case 'RtMktSumNonRtStat':
        if (gDiag.wsOnFirstRtMktSumNonRtStatTime === minDate) {
          gDiag.wsOnFirstRtMktSumNonRtStatTime = new Date();
          // console.log('sq.d: ' + gDiag.wsOnFirstRtMktSumNonRtStatTime.toISOString() + ': wsOnFirstRtMktSumNonRtStatTime()'); // called 17ms after main.ts
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
        const msgStrNonRt = jsonArrayObjNonRt.map(s => s.assetId + '-' + s.ticker + ':prevClose-' + s.previousClose.toFixed(2).toString() + ' : periodStart-' + s.periodStart.toString() + ':open-' + s.periodOpen.toFixed(2).toString() + '/high-' + s.periodHigh.toFixed(2).toString() + '/low-' + s.periodLow.toFixed(2).toString() + '/mdd' + s.periodMaxDD.toFixed(2).toString() + '/mdu' + s.periodMaxDU.toFixed(2).toString()).join(', ');
        console.log('ws: RtMktSumNonRtStat arrived: ' + msgStrNonRt);
        this.rtMktSumPeriodStatStr = msgStrNonRt;

        this.updateMktSumNonRt(jsonArrayObjNonRt, this.marketFullStat);
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

    console.log('StartDate: ' + this.lookbackStart);

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
    //       this.rtMktSumPeriodStatStr = msgStr;
    //     });
    // }
  }

  updateMktSumRt(message: RtMktSumRtStat[], marketFullStat: RtMktSumFullStat[]): void {
    for (const singleStockInfo of message) {
      const existingFullStatItems = marketFullStat.filter(fullStatItem => fullStatItem.assetId === singleStockInfo.assetId);
      if (existingFullStatItems.length === 0) {
        marketFullStat.push({assetId: singleStockInfo.assetId, ticker: '', last: singleStockInfo.last, previousClose: NaN, periodStart: new Date(), periodOpen: NaN, periodHigh: NaN,
                          periodLow: NaN, dailyReturn: NaN, periodReturn: NaN, drawDownPct: NaN, drawUpPct: NaN, maxDrawDownPct: NaN, maxDrawUpPct: NaN});
      } else {
        existingFullStatItems[0].last = singleStockInfo.last;
        this.updateReturns(existingFullStatItems[0]);
      }
    }

    this.updateTableRows(marketFullStat);
    this.makeUiTableVisible();
  }

  updateMktSumNonRt(message: RtMktSumNonRtStat[], marketFullStat: RtMktSumFullStat[]): void {
    for (const singleStockInfo of message) {
      const existingFullStatItems = marketFullStat.filter(fullStatItem => fullStatItem.assetId === singleStockInfo.assetId);
      if (existingFullStatItems.length === 0) {
        marketFullStat.push({
          assetId: singleStockInfo.assetId, ticker: singleStockInfo.ticker, last: NaN, previousClose: singleStockInfo.previousClose, periodStart: singleStockInfo.periodStart, periodOpen: singleStockInfo.periodOpen, periodHigh: singleStockInfo.periodHigh,
          periodLow: singleStockInfo.periodLow, dailyReturn: NaN, periodReturn: NaN, drawDownPct: NaN, drawUpPct: NaN, maxDrawDownPct: singleStockInfo.periodMaxDD, maxDrawUpPct: singleStockInfo.periodMaxDU
        });
        this.tableHeaderLinks.push({ ticker: singleStockInfo.ticker, reference: 'https://uk.tradingview.com/chart/?symbol=' + singleStockInfo.ticker });
      } else {
        existingFullStatItems[0].ticker = singleStockInfo.ticker;
        existingFullStatItems[0].previousClose = singleStockInfo.previousClose;
        existingFullStatItems[0].periodStart = singleStockInfo.periodStart;
        existingFullStatItems[0].periodOpen = singleStockInfo.periodOpen;
        existingFullStatItems[0].periodHigh = singleStockInfo.periodHigh;
        existingFullStatItems[0].periodLow = singleStockInfo.periodLow;
        existingFullStatItems[0].maxDrawDownPct = singleStockInfo.periodMaxDD;
        existingFullStatItems[0].maxDrawUpPct = singleStockInfo.periodMaxDU;

        this.updateReturns(existingFullStatItems[0]);
      }
    }

    this.updateTableRows(marketFullStat);
    this.makeUiTableVisible();
  }

  updateReturns(item: RtMktSumFullStat) {
    item.dailyReturn = item.last > 0 ? item.last / item.previousClose - 1 : 0;
    item.periodReturn = item.last > 0 ? item.last / item.periodOpen - 1 : item.previousClose / item.periodOpen - 1;
    item.drawDownPct = item.last > 0 ? item.last / Math.max(item.periodHigh, item.last) - 1 : item.previousClose / item.periodHigh - 1;
    item.drawUpPct = item.last > 0 ? item.last / Math.min(item.periodLow, item.last) - 1 : item.previousClose / item.periodLow - 1;
    item.maxDrawDownPct = Math.min(item.maxDrawDownPct, item.drawDownPct);
    item.maxDrawUpPct = Math.max(item.maxDrawUpPct, item.drawUpPct);
  }

  updateTableRows(perfIndicators: RtMktSumFullStat[]) {
    this.perfIndDaily = [];
    this.perfIndPeriod = [];
    this.perfIndCDD = [];
    this.perfIndCDU = [];
    this.perfIndMDD = [];
    this.perfIndMDU = [];
    this.perfIndPeriodFull = [];
    console.log('StartD: ' + this.lookbackStart);

    for (const items of perfIndicators) {
      // NaN can be valid that means 'No Data' on server. UI can handle with a nicer "NoData" message than "NaN", but don't ignore that.
      // if (Number.isNaN(items.dailyReturn) === false) {
      this.perfIndDaily.push({ticker: items.ticker, dailyReturnStr: (items.dailyReturn >= 0 ? '+' : '') + (items.dailyReturn * 100).toFixed(2).toString() + '%', dailyReturnSign: Math.sign(items.dailyReturn), dailyReturnClass: (items.dailyReturn >= 0 ? 'positivePerf' : 'negativePerf'),
      dailyTooltipStr1: items.ticker,
      dailyTooltipStr2: 'Daily return: ' + (items.dailyReturn >= 0 ? '+' : '') + (items.dailyReturn * 100).toFixed(2).toString() + '%' + '\n' + 'Last price: ' + items.last.toFixed(2).toString() + '\n' + 'Previous close price: ' + items.previousClose.toFixed(2).toString()} );
      const dataStartDate: Date = new Date(items.periodStart);
      console.log(items.ticker + ' ' + dataStartDate);
      this.perfIndPeriodFull.push({ticker: items.ticker,
        periodReturnStr: (items.periodReturn >= 0 ? '+' : '') + (items.periodReturn * 100).toFixed(2).toString() + '%',
        drawDownPctStr: (items.drawDownPct >= 0 ? '+' : '') + (items.drawDownPct * 100).toFixed(2).toString() + '%',
        drawUpPctStr: (items.drawUpPct >= 0 ? '+' : '') + (items.drawUpPct * 100).toFixed(2).toString() + '%',
        maxDrawDownPctStr: (items.maxDrawDownPct >= 0 ? '+' : '') + (items.maxDrawDownPct * 100).toFixed(2).toString() + '%',
        maxDrawUpPctStr: (items.maxDrawUpPct >= 0 ? '+' : '') + (items.maxDrawUpPct * 100).toFixed(2).toString() + '%',
        selectedPerfIndSign: Math.sign(items.periodReturn),
        selectedPerfIndClass: '',
        selectedPerfIndStr: '',
        periodPerfTooltipStr1: '',
        periodPerfTooltipStr2: '',
        periodPerfTooltipStr3: '\r\n' + 'Period start: ' + dataStartDate.toISOString().slice(0, 10) + '\r\n' + 'Previous close: ' + items.previousClose.toFixed(2).toString() + '\r\n' + 'Period open: ' + items.periodOpen.toFixed(2).toString() + '\r\n' + 'Period high: ' + items.periodHigh.toFixed(2).toString() + '\r\n' + 'Period low: ' + items.periodLow.toFixed(2).toString(),
        lookbackErrorString: (dataStartDate > this.lookbackStart) ? ('! Period data starts on ' + dataStartDate.toISOString().slice(0, 10) + '\r\n' + ' instead of the expected ' + this.lookbackStart.toISOString().slice(0, 10)) + '. \r\n\r\n' : '',
        lookbackErrorClass: (dataStartDate > this.lookbackStart) ? 'lookbackError' : ''
      });
      // }
    }

    console.log('Start: "' + this.lookbackStart + '", VXX start: "' + perfIndicators[4].periodStart + '", class: ' + this.perfIndPeriodFull[4].lookbackErrorClass + ', errorStr: ' + this.perfIndPeriodFull[4].lookbackErrorString);

    for (const items of this.perfIndPeriodFull) {
      items.periodPerfTooltipStr1 = items.ticker;
      items.periodPerfTooltipStr2 = 'Period return: ' + items.periodReturnStr + '\r\n' + 'Current drawdown: ' + items.drawDownPctStr + '\r\n' + 'Current drawup: ' + items.drawUpPctStr + '\r\n' + 'Maximum drawdown: ' + items.maxDrawDownPctStr + '\r\n' + 'Maximum drawup: ' + items.maxDrawUpPctStr;
    }
    this.perfIndicatorSelector();
  }

  makeUiTableVisible() {
    // sometimes Rt, sometimes nonRt data arrives 300ms earlier than the other. Show table only when BOTH have arrived to avoid that cells blink in red for 300ms
    if (this.nRtStatArrived === 0 || this.nNonRtStatArrived === 0) {
      return;
    }
    const perfTableElement = document.getElementById('perfTable');
    if (typeof(perfTableElement) !== 'undefined' && perfTableElement !== null) {
      perfTableElement.style.visibility = 'visible';
    }
  }

}
