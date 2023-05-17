import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime, minDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { processUiWithPrtfRunResultChrt } from '../../../sq-ng-common/src/lib/chart/advanced-chart';
import { PrtfRunResultJs, UiChartPointValues, UiPrtfRunResult } from '../../../MarketDashboard/src/sq-globals';
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

  public serverBacktestStartTime: Date = minDate;
  public serverBacktestEndTime: Date = minDate;
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

  prtfRunResults: Nullable<PrtfRunResultJs[]> = [];
  uiPrtfRunResults: UiPrtfRunResult[] = [];
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
    gChrtGenDiag.serverBacktestStartTime = new Date();
    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/chrtgen' + wsQueryStr); // "wss://127.0.0.1/ws/chrtgen?pids=13,2" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection. ? has to be included to separate the location from the params
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
          gChrtGenDiag.serverBacktestEndTime = new Date();
          console.log('ChrtGen.PrtfRunResult:' + msgObjStr);
          this.processPortfolioRunResult(msgObjStr);
          gChrtGenDiag.totalUiResponseTime = new Date();
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
      AppComponent.updateUiWithPrtfRunResult(this.prtfRunResults, this.uiPrtfRunResults, this.pvChrtWidth, this.pvChrtHeight);
    });
  }

  processPortfolioRunResult(msgObjStr: string) {
    this.prtfRunResults = JSON.parse(msgObjStr, function(this: any, key, value) {
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
    AppComponent.updateUiWithPrtfRunResult(this.prtfRunResults, this.uiPrtfRunResults, this.pvChrtWidth, this.pvChrtHeight);
  }

  static updateUiWithPrtfRunResult(prtfRunResults: Nullable<PrtfRunResultJs[]>, uiPrtfRunResults: UiPrtfRunResult[], uiChrtWidth: number, uiChrtHeight: number) {
    if (prtfRunResults == null)
      return;

    uiPrtfRunResults.length = 0;
    for (const prtfResItem of prtfRunResults) {
      if (prtfResItem.pstat == null || prtfResItem.chart == null)
        continue;
      const uiPrtfResItem = new UiPrtfRunResult();
      uiPrtfResItem.startPortfolioValue = prtfResItem.pstat.startPortfolioValue;
      uiPrtfResItem.endPortfolioValue = prtfResItem.pstat.endPortfolioValue;
      uiPrtfResItem.totalReturn = prtfResItem.pstat.totalReturn;
      uiPrtfResItem.cAGR = parseFloat(prtfResItem.pstat.cagr);
      uiPrtfResItem.maxDD = parseFloat(prtfResItem.pstat.maxDD);
      uiPrtfResItem.sharpeRatio = prtfResItem.pstat.sharpeRatio;
      uiPrtfResItem.stDev = parseFloat(prtfResItem.pstat.stDev);
      // uiPrtfResItem.ulcer = parseFloat(prtfItem.pstat.ulcer); // yet to calcualte
      uiPrtfResItem.tradingDays = parseInt(prtfResItem.pstat.tradingDays);
      uiPrtfResItem.nTrades = parseInt(prtfResItem.pstat.nTrades);
      uiPrtfResItem.winRate = parseFloat(prtfResItem.pstat.winRate);
      uiPrtfResItem.lossRate = parseFloat(prtfResItem.pstat.lossingRate);
      uiPrtfResItem.sortino = prtfResItem.pstat.sortino;
      uiPrtfResItem.turnover = parseFloat(prtfResItem.pstat.turnover);
      uiPrtfResItem.longShortRatio = parseFloat(prtfResItem.pstat.longShortRatio);
      uiPrtfResItem.fees = parseFloat(prtfResItem.pstat.fees);
      // uiPrtfResItem.benchmarkCAGR = parseFloat(prtfItem.pstat.benchmarkCAGR); // yet to calcualte
      // uiPrtfResItem.benchmarkMaxDD = parseFloat(prtfItem.pstat.benchmarkMaxDD); // yet to calcualte
      // uiPrtfResItem.correlationWithBenchmark = parseFloat(prtfItem.pstat.correlationWithBenchmark); // yet to calcualte
      for (let j = 0; j < prtfResItem.chart.dates.length; j++) {
        const chartItem = new UiChartPointValues();
        const mSecSinceUnixEpoch: number = prtfResItem.chart.dates[j] * 1000; // data comes as seconds. JS uses milliseconds since Epoch.
        chartItem.dates = new Date(mSecSinceUnixEpoch);
        chartItem.values = prtfResItem.chart.values[j];
        uiPrtfResItem.chrtValues.push(chartItem);
      }
      uiPrtfRunResults.push(uiPrtfResItem);
    }

    d3.selectAll('#pfRunResultChrt > *').remove();
    const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
    const margin = {top: 50, right: 50, bottom: 30, left: 60 };
    const chartWidth = uiChrtWidth * 0.9 - margin.left - margin.right; // 90% of the PanelChart Width
    const chartHeight = uiChrtHeight * 0.9 - margin.top - margin.bottom; // 90% of the PanelChart Height
    const chrtData = uiPrtfRunResults[0].chrtValues.map((r:{ dates: Date; values: number; }) => ({date: new Date(r.dates), value: r.values}));
    const xMin = d3.min(chrtData, (r:{ date: Date; }) => r.date);
    const xMax = d3.max(chrtData, (r:{ date: Date; }) => r.date);
    const yMinAxis = d3.min(chrtData, (r:{ value: number; }) => r.value);
    const yMaxAxis = d3.max(chrtData, (r:{ value: number; }) => r.value);

    processUiWithPrtfRunResultChrt(chrtData, lineChrtDiv, chartWidth, chartHeight, margin, xMin, xMax, yMinAxis, yMaxAxis);
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
        `Server backtest time: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.serverBacktestStartTime, gChrtGenDiag.serverBacktestEndTime)}\n` +
        `Total UI response: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.serverBacktestStartTime, gChrtGenDiag.totalUiResponseTime)}\n`;
        // `Communication Overhead: ${(chrtGenDiag.mainTsTime.getTime() - chrtGenDiag.totalUiResponseTime.getTime()) - (chrtGenDiag.mainTsTime.getTime() - chrtGenDiag.serverBacktestTime.getTime()) +'ms'}\n`;
      } else
        this.chrtGenDiagnosticsMsg = 'Connection to server is broken.\n Try page reload (F5).';
    }
  }
}