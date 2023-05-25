import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime, minDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { processUiWithPrtfRunResultChrt } from '../../../sq-ng-common/src/lib/chart/advanced-chart';
import { ChrtGenBacktestResult, UiChartPointValues, UiChrtGenPrtfRunResult } from '../../../MarketDashboard/src/sq-globals';
import * as d3 from 'd3';

type Nullable<T> = T | null;

class HandshakeMessage {
  public email = '';
  public param2 = '';
}

export class ChrtGenDiagnostics { // have to export the class, because .mainTsTime is set from outside of this angular component.
  public mainTsTime: Date = new Date();
  public mainAngComponentConstructorTime: Date = new Date();
  public windowOnLoadTime: Date = minDate;

  public serverBacktestTime: number = 0;
  public communicationOverheadTime: string = '';
  public totalUiResponseTime: Date = minDate;
}

export const gChrtGenDiag: ChrtGenDiagnostics = new ChrtGenDiagnostics();

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  m_http: HttpClient;

  chrtGenBacktestResults: Nullable<ChrtGenBacktestResult> = null;
  uiChrtGenPrtfRunResults: UiChrtGenPrtfRunResult[] = [];
  pvChrtWidth = 0;
  pvChrtHeight = 0;

  prtfIds: string = '';
  bmrks: string = ''; // benchmarks
  isSrvConnectionAlive: boolean = true;
  chrtGenDiagnosticsMsg = 'Benchmarking time, connection speed';

  user = {
    name: 'Anonymous',
    email: '             '
  };
  public activeTool = 'ChartGenerator';
  public _socket: WebSocket; // initialize later in ctor, becuse we have to send back the activeTool from urlQueryParams

  constructor(http: HttpClient) {
    gChrtGenDiag.mainAngComponentConstructorTime = new Date();
    this.m_http = http;

    const wsQueryStr = window.location.search; // https://sqcore.net/webapps/ChartGenerator/?pids=1  , but another parameter example can be pids=1,13,6&bmrks=SPY,QQQ&start=20210101&end=20220305
    console.log(wsQueryStr);
    // gChrtGenDiag.communicationStartTime = new Date();
    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/chrtgen' + wsQueryStr); // "wss://127.0.0.1/ws/chrtgen?pids=13,2" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection. ? has to be included to separate the location from the params

    setInterval(() => { // checking whether the connection is live or not
      this.isSrvConnectionAlive = this._socket != null && this._socket.readyState === WebSocket.OPEN;
    }, 5 * 1000); // refresh at every 5 secs
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
        case 'BacktestResults':
          // gChrtGenDiag.communicationOverheadTime = (new Date().getTime() - gChrtGenDiag.communicationStartTime.getTime()).toString();
          console.log('ChrtGen.BacktestResults:' + msgObjStr);
          this.processChrtGenBacktestResults(msgObjStr);
          break;
        case 'ErrorToUser':
          console.log('ChrtGen.ErrorToUser:' + msgObjStr);
          break;
        default:
          return false;
      }
    };
    const backtestResChartId = SqNgCommonUtils.getNonNullDocElementById('backtestPvChrt');
    this.pvChrtWidth = backtestResChartId.clientWidth as number;
    this.pvChrtHeight = backtestResChartId.clientHeight as number;
    // resizing the chart dynamically when the window is resized
    window.addEventListener('resize', () => {
      this.pvChrtWidth = backtestResChartId.clientWidth as number; // we have to remember the width/height every time window is resized, because we give these to the chart
      this.pvChrtHeight = backtestResChartId.clientHeight as number;
      AppComponent.updateUiWithChrtGenBacktestResults(this.chrtGenBacktestResults, this.uiChrtGenPrtfRunResults, this.pvChrtWidth, this.pvChrtHeight);
    });
  }

  processChrtGenBacktestResults(msgObjStr: string) {
    this.chrtGenBacktestResults = JSON.parse(msgObjStr, function(this: any, key, value) {
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
    AppComponent.updateUiWithChrtGenBacktestResults(this.chrtGenBacktestResults, this.uiChrtGenPrtfRunResults, this.pvChrtWidth, this.pvChrtHeight);
  }

  static updateUiWithChrtGenBacktestResults(chrtGenBacktestRes: Nullable<ChrtGenBacktestResult>, uiChrtGenPrtfRunResults: UiChrtGenPrtfRunResult[], uiChrtWidth: number, uiChrtHeight: number) {
    if (chrtGenBacktestRes == null || chrtGenBacktestRes.pfRunResults == null)
      return;

    uiChrtGenPrtfRunResults.length = 0;
    const uiPrtfResItem = new UiChrtGenPrtfRunResult();
    gChrtGenDiag.serverBacktestTime = chrtGenBacktestRes.serverBacktestTimeMs;
    for (const item of chrtGenBacktestRes.pfRunResults) {
      uiPrtfResItem.startPortfolioValue = item.pstat.startPortfolioValue;
      uiPrtfResItem.endPortfolioValue = item.pstat.endPortfolioValue;
      uiPrtfResItem.totalReturn = item.pstat.totalReturn;
      uiPrtfResItem.cAGR = parseFloat(item.pstat.cagr);
      uiPrtfResItem.maxDD = parseFloat(item.pstat.maxDD);
      uiPrtfResItem.sharpeRatio = item.pstat.sharpeRatio;
      uiPrtfResItem.stDev = parseFloat(item.pstat.stDev);
      // uiPrtfResItem.ulcer = parseFloat(item.pstat.ulcer); // yet to calcualte
      uiPrtfResItem.tradingDays = parseInt(item.pstat.tradingDays);
      uiPrtfResItem.nTrades = parseInt(item.pstat.nTrades);
      uiPrtfResItem.winRate = parseFloat(item.pstat.winRate);
      uiPrtfResItem.lossRate = parseFloat(item.pstat.lossingRate);
      uiPrtfResItem.sortino = item.pstat.sortino;
      uiPrtfResItem.turnover = parseFloat(item.pstat.turnover);
      uiPrtfResItem.longShortRatio = parseFloat(item.pstat.longShortRatio);
      uiPrtfResItem.fees = parseFloat(item.pstat.fees);
      // uiPrtfResItem.benchmarkCAGR = parseFloat(item.pstat.benchmarkCAGR); // yet to calcualte
      // uiPrtfResItem.benchmarkMaxDD = parseFloat(item.pstat.benchmarkMaxDD); // yet to calcualte
      // uiPrtfResItem.correlationWithBenchmark = parseFloat(item.pstat.correlationWithBenchmark); // yet to calcualte

      uiPrtfResItem.chrtResolution = item.chartResolution;

      for (let i = 0; i < item.chart.dates.length; i++) {
        const chartItem = new UiChartPointValues();
        const mSecSinceUnixEpoch: number = item.chart.dates[i] * 1000; // data comes as seconds. JS uses milliseconds since Epoch.
        chartItem.dates = new Date(mSecSinceUnixEpoch);
        chartItem.values = item.chart.values[i];
        uiPrtfResItem.chrtValues.push(chartItem);
      }
    }

    for (const bmrkItem of chrtGenBacktestRes.bmrkHistories) { // processing benchamrk History data
      for (let i = 0; i < bmrkItem.histPrices.date.length; i++) {
        const chartItem = new UiChartPointValues();
        const dateStr: string = bmrkItem.histPrices.date[i];
        chartItem.dates = new Date(dateStr.substring(0, 4) + '-' + dateStr.substring(5, 7) + '-' + dateStr.substring(8, 10));
        chartItem.values = bmrkItem.histPrices.price[i];
        uiPrtfResItem.bmrkChrtValues.push(chartItem);
      }
    }
    uiChrtGenPrtfRunResults.push(uiPrtfResItem);

    d3.selectAll('#pfRunResultChrt > *').remove();
    const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
    const margin = {top: 50, right: 50, bottom: 30, left: 60 };
    const chartWidth = uiChrtWidth * 0.9 - margin.left - margin.right; // 90% of the PvChart Width
    const chartHeight = uiChrtHeight * 0.9 - margin.top - margin.bottom; // 90% of the PvChart Height
    const chrtData = uiChrtGenPrtfRunResults[0].chrtValues.map((r:{ dates: Date; values: number; }) => ({date: new Date(r.dates), value: r.values}));
    const xMin = d3.min(chrtData, (r:{ date: Date; }) => r.date);
    const xMax = d3.max(chrtData, (r:{ date: Date; }) => r.date);
    const yMinAxis = d3.min(chrtData, (r:{ value: number; }) => r.value);
    const yMaxAxis = d3.max(chrtData, (r:{ value: number; }) => r.value);

    processUiWithPrtfRunResultChrt(chrtData, lineChrtDiv, chartWidth, chartHeight, margin, xMin, xMax, yMinAxis, yMaxAxis);

    d3.selectAll('#bmrkChrt > *').remove(); // we can modify the method for pfRunResChrt and BmrkChrt into one single method - Daya
    const bmkrklineChrtDiv = document.getElementById('bmrkChrt') as HTMLElement;
    const bmrkMargin = {top: 50, right: 50, bottom: 30, left: 60 };
    const bmrkChartWidth = uiChrtWidth * 0.9 - bmrkMargin.left - bmrkMargin.right; // 90% of the BmrkChart Width
    const bmrkChartHeight = uiChrtHeight * 0.9 - bmrkMargin.top - bmrkMargin.bottom; // 90% of the Chart Height
    const bmrkChrtData = uiChrtGenPrtfRunResults[0].bmrkChrtValues.map((r:{ dates: Date; values: number; }) => ({date: new Date(r.dates), value: r.values}));
    const bmrkXMin = d3.min(bmrkChrtData, (r:{ date: Date; }) => r.date);
    const bmrkXMax = d3.max(bmrkChrtData, (r:{ date: Date; }) => r.date);
    const bmrkYMinAxis = d3.min(bmrkChrtData, (r:{ value: number; }) => r.value);
    const bmrkYMaxAxis = d3.max(bmrkChrtData, (r:{ value: number; }) => r.value);

    processUiWithPrtfRunResultChrt(bmrkChrtData, bmkrklineChrtDiv, bmrkChartWidth, bmrkChartHeight, bmrkMargin, bmrkXMin, bmrkXMax, bmrkYMinAxis, bmrkYMaxAxis);
  }

  onStartBacktests() {
    if (this._socket != null && this._socket.readyState === this._socket.OPEN)
      this._socket.send('RunBacktest:' + '?pids='+ this.prtfIds + '&bmrks=' + this.bmrks); // parameter example can be pids=1,13,6&bmrks=SPY,QQQ&start=20210101&end=20220305
  }

  // "Server backtest time: 300ms, Communication overhead: 120ms, Total UI response: 420ms."
  mouseEnter(div: string) { // giving some data to display - Daya
    if (div === 'chrtGenDiagnosticsMsg') {
      if (this.isSrvConnectionAlive) {
        this.chrtGenDiagnosticsMsg = `App constructor: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.mainTsTime, gChrtGenDiag.mainAngComponentConstructorTime)}\n` +
        `Window loaded: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.mainTsTime, gChrtGenDiag.windowOnLoadTime)}\n` +
        '-----\n' +
        `Server backtest time: ${gChrtGenDiag.serverBacktestTime + 'ms' }\n`;
        // `Communication Overhead: ${gChrtGenDiag.communicationOverheadTime +'ms'}\n` +
        // `Total UI response: ${gChrtGenDiag.serverBacktestTime + parseInt(gChrtGenDiag.communicationOverheadTime) +'ms'}\n`;
      } else
        this.chrtGenDiagnosticsMsg = 'Connection to server is broken.\n Try page reload (F5).';
    }
  }
}