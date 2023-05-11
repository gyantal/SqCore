import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { processUiWithPrtfRunResultChrt } from '../../../sq-ng-common/src/lib/chart/advanced-chart';
import { gDiag, PrtfRunResultJs, UiChartPointValues, UiPrtfRunResult } from '../../../MarketDashboard/src/sq-globals';
import * as d3 from 'd3';

type Nullable<T> = T | null;

class HandshakeMessage {
  public email = '';
  public param2 = '';
}

// const minDate = new Date();

// export class ChrtGenDiagnostics {
//   public mainTsTime: Date = new Date();
//   public mainAngComponentConstructorTime: Date = minDate;
//   public windowOnLoadTime: Date = minDate;

//   public serverBacktestTime: Date = minDate;
//   public communicationOverheadTime: Date = minDate;
//   public totalUiResponseTime: Date = minDate;
// }

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  m_http: HttpClient;
  m_portfolioId = -1; // -1 is invalid ID

  prtfRunResult: Nullable<PrtfRunResultJs> = null;
  uiPrtfRunResult: UiPrtfRunResult = new UiPrtfRunResult();
  chrtWidth = 0;
  chrtHeight = 0;

  isSrvConnectionAlive: boolean = true;
  chrtGenDiagnosticsMsg = 'Benchmarking time, connection speed';

  // UrlQueryParams (keep them short): // ?t=bav
  public urlQueryParamsArr : string[][];
  public urlQueryParamsObj = {}; // empty object. If queryParamsObj['t'] doesn't exist, it returns 'undefined'
  user = {
    name: 'Anonymous',
    email: '             '
  };
  public activeTool = 'ChartGenerator';
  public _socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams

  constructor(http: HttpClient) {
    gDiag.mainAngComponentConstructorTime = new Date();
    this.m_http = http;

    const url = new URL(window.location.href); // https://sqcore.net/webapps/ChartGenerator/?id=1
    // const prtfIdStr = url.searchParams.get('pids=1,13,6&bnchks=SPY,QQQ');
    const prtfIdStr = url.searchParams.get('id');
    if (prtfIdStr != null)
      this.m_portfolioId = parseInt(prtfIdStr);

    this.urlQueryParamsArr = SqNgCommonUtils.getUrlQueryParamsArray();
    this.urlQueryParamsObj = SqNgCommonUtils.Array2Obj(this.urlQueryParamsArr);
    console.log('AppComponent.ctor: queryParamsArr.Length: ' + this.urlQueryParamsArr.length);
    console.log('AppComponent.ctor: Active Tool, queryParamsObj["t"]: ' + this.urlQueryParamsObj['t']);

    let wsQueryStr = '';
    const paramActiveTool = this.urlQueryParamsObj['id'];
    if (paramActiveTool != undefined && paramActiveTool != 'mh') // if it is not missing and not the default active tool: MarketHealth
      wsQueryStr = '?pid:' + paramActiveTool; // ?pid:

    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/chrtgen' + wsQueryStr); // "wss://127.0.0.1/ws/dashboard" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection
  }

  ngOnInit(): void {
    // WebSocket connection

    this._socket.onopen = () => {
      console.log('ws: Connection started! _socket.send() can be used now.');
    };

    this._socket.onmessage = (event) => {
      const semicolonInd = event.data.indexOf(':');
      const msgCode = event.data.slice(0, semicolonInd);
      const msgObjStr = event.data.substring(semicolonInd + 1);
      switch (msgCode) {
        case 'OnConnected':
          console.log('ws: OnConnected message arrived:' + event.data);
          const handshakeMsg: HandshakeMessage = Object.assign(new HandshakeMessage(), JSON.parse(msgObjStr));
          this.user.email = handshakeMsg.email;
          break;
        case 'PrtfRunResult':
          console.log('ChrtGen.PrtfRunResult:' + msgObjStr);
          this.processPortfolioRunResult(msgObjStr);
          break;
        default:
          return false;
      }
    };

    const backtestResChartId = AppComponent.getNonNullDocElementById('backtestResChrt');
    this.chrtWidth = backtestResChartId.clientWidth as number;
    this.chrtHeight = backtestResChartId.clientHeight as number;
    // resizing the chart dynamically based on window size
    window.addEventListener('resize', () => {
      this.chrtWidth = backtestResChartId.clientWidth as number;
      this.chrtHeight = backtestResChartId.clientHeight as number;
      AppComponent.updateUiWithPrtfRunResult(this.prtfRunResult, this.uiPrtfRunResult, this.chrtWidth, this.chrtHeight);
    });
  }

  static getNonNullDocElementById(id: string): HTMLElement { // document.getElementById() can return null. This 'forced' type casting fakes that it is not null for the TS compiler. (it can be null during runtime)
    return document.getElementById(id) as HTMLElement;
  }

  processPortfolioRunResult(msgObjStr: string) {
    this.prtfRunResult = JSON.parse(msgObjStr, function(this: any, key, value) {
      // property names and values are transformed to a shorter ones for decreasing internet traffic.Transform them back to normal for better code reading.

      // 'this' is the object containing the property being processed (not the embedding class) as this is a function(), not a '=>', and the property name as a string, the property value as arguments of this function.
      // eslint-disable-next-line no-invalid-this
      const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

      if (key === 'startPv') {
        _this.startPortfolioValue = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'endPv') {
        _this.endPortfolioValue = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'shrp') {
        _this.sharpeRatio = value == 'NaN' ? NaN : parseFloat(value);
        return; // if return undefined, original property will be removed
      }
      if (key === 'tr') {
        _this.totalReturn = parseFloat(value);
        return; // if return undefined, original property will be removed
      }
      if (key === 'wr') {
        _this.winRate = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'lr') {
        _this.lossingRate = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'srtn') {
        _this.sortino = value == 'NaN' ? NaN : parseFloat(value);
        return; // if return undefined, original property will be removed
      }
      if (key === 'to') {
        _this.turnover = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'ls') {
        _this.longShortRatio = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'bCAGR') {
        _this.benchmarkCAGR = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'bMax') {
        _this.benchmarkMaxDD = value;
        return; // if return undefined, original property will be removed
      }
      if (key === 'cwb') {
        _this.correlationWithBenchmark = value;
        return; // if return undefined, original property will be removed
      }
      return value;
    });
    AppComponent.updateUiWithPrtfRunResult(this.prtfRunResult, this.uiPrtfRunResult, this.chrtWidth, this.chrtHeight);
  }

  static updateUiWithPrtfRunResult(prtfRunResult: Nullable<PrtfRunResultJs>, uiPrtfRunResult: UiPrtfRunResult, uiChrtWidth: number, uiChrtHeight: number) {
    if (prtfRunResult == null)
      return;

    uiPrtfRunResult.startPortfolioValue = prtfRunResult.pstat.startPortfolioValue;
    uiPrtfRunResult.endPortfolioValue = prtfRunResult.pstat.endPortfolioValue;
    uiPrtfRunResult.totalReturn = prtfRunResult.pstat.totalReturn;
    uiPrtfRunResult.cAGR = parseFloat(prtfRunResult.pstat.cagr);
    uiPrtfRunResult.maxDD = parseFloat(prtfRunResult.pstat.maxDD);
    uiPrtfRunResult.sharpeRatio = prtfRunResult.pstat.sharpeRatio;
    uiPrtfRunResult.stDev = parseFloat(prtfRunResult.pstat.stDev);
    // uiPrtfRunResult.ulcer = parseFloat(prtfRunResult.pstat.ulcer); // yet to calcualte
    uiPrtfRunResult.tradingDays = parseInt(prtfRunResult.pstat.tradingDays);
    uiPrtfRunResult.nTrades = parseInt(prtfRunResult.pstat.nTrades);
    uiPrtfRunResult.winRate = parseFloat(prtfRunResult.pstat.winRate);
    uiPrtfRunResult.lossRate = parseFloat(prtfRunResult.pstat.lossingRate);
    uiPrtfRunResult.sortino = prtfRunResult.pstat.sortino;
    uiPrtfRunResult.turnover = parseFloat(prtfRunResult.pstat.turnover);
    uiPrtfRunResult.longShortRatio = parseFloat(prtfRunResult.pstat.longShortRatio);
    uiPrtfRunResult.fees = parseFloat(prtfRunResult.pstat.fees);
    // uiPrtfRunResult.benchmarkCAGR = parseFloat(prtfRunResult.pstat.benchmarkCAGR); // yet to calcualte
    // uiPrtfRunResult.benchmarkMaxDD = parseFloat(prtfRunResult.pstat.benchmarkMaxDD); // yet to calcualte
    // uiPrtfRunResult.correlationWithBenchmark = parseFloat(prtfRunResult.pstat.correlationWithBenchmark); // yet to calcualte

    uiPrtfRunResult.chrtValues.length = 0;
    for (let i = 0; i < prtfRunResult.chart.dates.length; i++) {
      const chartItem = new UiChartPointValues();
      const mSecSinceUnixEpoch: number = prtfRunResult.chart.dates[i] * 1000; // data comes as seconds. JS uses milliseconds since Epoch.
      chartItem.dates = new Date(mSecSinceUnixEpoch);
      chartItem.values = prtfRunResult.chart.values[i];
      uiPrtfRunResult.chrtValues.push(chartItem);
    }

    d3.selectAll('#pfRunResultChrt > *').remove();
    const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
    const margin = {top: 50, right: 50, bottom: 30, left: 60 };
    const chartWidth = uiChrtWidth * 0.9 - margin.left - margin.right; // 90% of the PanelChart Width
    const chartHeight = uiChrtHeight * 0.9 - margin.top - margin.bottom; // 90% of the PanelChart Height
    const chrtData = uiPrtfRunResult.chrtValues.map((r:{ dates: Date; values: number; }) => ({date: new Date(r.dates), value: r.values}));
    const xMin = d3.min(chrtData, (r:{ date: Date; }) => r.date);
    const xMax = d3.max(chrtData, (r:{ date: Date; }) => r.date);
    const yMinAxis = d3.min(chrtData, (r:{ value: number; }) => r.value);
    const yMaxAxis = d3.max(chrtData, (r:{ value: number; }) => r.value);

    processUiWithPrtfRunResultChrt(chrtData, lineChrtDiv, chartWidth, chartHeight, margin, xMin, xMax, yMinAxis, yMaxAxis);
  }

  onRunBacktestClicked() {
    if (this._socket != null && this._socket.readyState === this._socket.OPEN)
      this._socket.send('RunBacktest:');
  }

  // "Server backtest time: 300ms, Communication overhead: 120ms, Total UI response: 420ms."
  mouseEnter(div: string) { // giving some data to display - Daya
    if (div === 'chrtGenDiagnosticsMsg') {
      if (this.isSrvConnectionAlive) {
        this.chrtGenDiagnosticsMsg = `App constructor: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.mainAngComponentConstructorTime)}\n` +
        `Server backtest time: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsConnectionStartTime)}\n` +
        `Communication Overhead: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsOnConnectedMsgArrivedTime)}\n` +
        `Total UI response: ${SqNgCommonUtilsTime.getTimespanStr(gDiag.mainTsTime, gDiag.wsOnConnectedMsgArrivedTime)}\n`;
      } else
        this.chrtGenDiagnosticsMsg = 'Connection to server is broken.\n Try page reload (F5).';
    }
  }
}