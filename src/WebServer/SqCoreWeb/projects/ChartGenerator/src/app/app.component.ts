import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime, minDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { chrtGenBacktestChrt } from '../../../../TsLib/sq-common/chartUltimate';
import { ChrtGenBacktestResult, UiChrtGenPrtfRunResult, UiChrtGenValue, SqLog, ChartResolution, UiChartPointValue } from '../../../../TsLib/sq-common/backtestCommon';
import { sleep } from '../../../../TsLib/sq-common/utils-common';
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

  public backtestRequestStartTime: Date = new Date();
  public backtestRequestReturnTime: Date = new Date();
  public serverBacktestTime: number = 0; // msec
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

  prtfIds: Nullable<string> = null;
  bmrks: Nullable<string> = null; // benchmarks
  startDate: Date = new Date(); // used to filter the chart Data based on the user input
  endDate: Date = new Date(); // used to filter the chart Data based on the user input
  startDateStr: string = '';
  endDateStr: string = '';
  rangeSelection = ['YTD', '1M', '1Y', '3Y', '5Y'];
  histRangeSelected: string = 'YTD';
  isSrvConnectionAlive: boolean = true;
  chrtGenDiagnosticsMsg = 'Benchmarking time, connection speed';
  isProgressBarVisble: boolean = false;
  isBacktestReturned: boolean = false;
  prtfOrBenchmark: string[] = ['SPY', 'TLT', 'RootUser', 'DualMomentum', 'VXX'];
  // Dummy Data - To be Deleted
  Data = [
    { name: 'TotalReturn', values: { 'SPY': '1%', 'TLT': '2%', 'RootUser': '1.5%', 'DualMomentum': '2%', 'VXX': '2%' } },
    { name: 'CAGR', values: { 'SPY': '2%', 'TLT': '5%', 'RootUser': '1.7%', 'DualMomentum': '2%', 'VXX': '2%' } },
    { name: 'StDev', values: { 'SPY': '1%', 'TLT': '2%', 'RootUser': '1.8%', 'DualMomentum': '4%', 'VXX': '2%' } },
    { name: 'Sharpe', values: { 'SPY': '2%', 'TLT': '5%', 'RootUser': '2.8%', 'DualMomentum': '2.5%', 'VXX': '2%' } },
    { name: 'MaxDD', values: { 'SPY': '1%', 'TLT': '2%', 'RootUser': '2%', 'DualMomentum': '3%', 'VXX': '2%' } }
  ];

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
    // Getting the PrtfIds and Benchmarks from URL
    const url = new URL(window.location.href);
    this.prtfIds = url.searchParams.get('pids');
    this.bmrks = url.searchParams.get('bmrks');

    // this.onStartBacktests(); - Testing
    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/chrtgen' + wsQueryStr); // "wss://127.0.0.1/ws/chrtgen?pids=13,2" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection. ? has to be included to separate the location from the params

    setInterval(() => { // checking whether the connection is live or not
      this.isSrvConnectionAlive = this._socket != null && this._socket.readyState === WebSocket.OPEN;
    }, 5 * 1000); // refresh at every 5 secs
  }

  ngOnInit(): void {
    // WebSocket connection
    this._socket.onopen = () => {
      console.log('ws: Connection started! _socket.send() can be used now.');
      this.showProgressBar();
    };

    this._socket.onmessage = async (event) => {
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
          if (gChrtGenDiag.serverBacktestTime) // check : serverBacktest Returned or not
            this.isBacktestReturned = true;
          else
            this.isBacktestReturned = false;
          console.log('ChrtGen.BacktestResults:' + msgObjStr);
          this.onCompleteBacktests(msgObjStr);
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
      AppComponent.updateUiWithChrtGenBacktestResults(this.chrtGenBacktestResults, this.uiChrtGenPrtfRunResults, this.pvChrtWidth, this.pvChrtHeight, this.startDate, this.endDate);
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
    AppComponent.updateUiWithChrtGenBacktestResults(this.chrtGenBacktestResults, this.uiChrtGenPrtfRunResults, this.pvChrtWidth, this.pvChrtHeight, this.startDate, this.endDate);
  }

  // startdate and enddate are not utlized at the moment - Daya yet to develop
  static updateUiWithChrtGenBacktestResults(chrtGenBacktestRes: Nullable<ChrtGenBacktestResult>, uiChrtGenPrtfRunResults: UiChrtGenPrtfRunResult[], uiChrtWidth: number, uiChrtHeight: number, startDate: Date, endDate: Date) {
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

      // uiPrtfResItem.totalReturn = item.chrtData.values[0] / item.chrtData.values[item.chrtData.values.length - 1];
      const chartItem = new UiChrtGenValue();
      chartItem.name = item.prtfName;
      chartItem.chartResolution = ChartResolution[item.chrtData.chartResolution];
      chartItem.priceData = [];
      for (let i = 0; i < item.chrtData.dates.length; i++) {
        const chrtItem = new UiChartPointValue();
        const mSecSinceUnixEpoch: number = item.chrtData.dates[i] * 1000; // data comes as seconds. JS uses milliseconds since Epoch.
        chrtItem.date = new Date(mSecSinceUnixEpoch);
        chrtItem.value = 100 * item.chrtData.values[i] / item.chrtData.values[0]; // used to convert the data into percentage values
        chartItem.priceData.push(chrtItem);
      }
      uiPrtfResItem.prtfChrtValues.push(chartItem);
    }

    for (const bmrkItem of chrtGenBacktestRes.bmrkHistories) { // processing benchamrk History data
      const chartItem = new UiChrtGenValue();
      chartItem.name = bmrkItem.sqTicker;
      chartItem.priceData = [];
      for (let i = 0; i < bmrkItem.histPrices.dates.length; i++) {
        const chrtItem = new UiChartPointValue();
        const dateStr: string = bmrkItem.histPrices.dates[i];
        chrtItem.date = new Date(dateStr.substring(0, 4) + '-' + dateStr.substring(5, 7) + '-' + dateStr.substring(8, 10));
        chrtItem.value = 100 * bmrkItem.histPrices.prices[i] / bmrkItem.histPrices.prices[0]; // used to convert the data into percentage values
        chartItem.priceData.push(chrtItem);
      }
      uiPrtfResItem.bmrkChrtValues.push(chartItem);
    }

    for (const item of chrtGenBacktestRes.logs) {
      const logItem = new SqLog();
      logItem.sqLogLevel = item.sqLogLevel;
      logItem.message = item.message;
      uiPrtfResItem.sqLogs.push(logItem);
    }

    uiChrtGenPrtfRunResults.push(uiPrtfResItem);

    d3.selectAll('#pfRunResultChrt > *').remove();
    const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
    const margin = {top: 50, right: 50, bottom: 30, left: 60 };
    const chartWidth = uiChrtWidth * 0.9 - margin.left - margin.right; // 90% of the PvChart Width
    const chartHeight = uiChrtHeight * 0.9 - margin.top - margin.bottom; // 90% of the PvChart Height
    const prtfAndBmrkChrtData: UiChrtGenValue[] = uiPrtfResItem.prtfChrtValues.concat(uiPrtfResItem.bmrkChrtValues);
    const lineChrtTooltip = document.getElementById('tooltipChart') as HTMLElement;

    chrtGenBacktestChrt(prtfAndBmrkChrtData, lineChrtDiv, chartWidth, chartHeight, margin, lineChrtTooltip, startDate, endDate);
  }

  async onStartBacktests() {
    gChrtGenDiag.backtestRequestStartTime = new Date();
    // Remember to Show Progress bar in 2 seconds from this time.
    await sleep(2000);
    if (!this.isBacktestReturned) // If the backtest hasn't returned yet (still pending), show Progress bar
      this.showProgressBar();
  }

  onCompleteBacktests(msgObjStr: string) {
    gChrtGenDiag.backtestRequestReturnTime = new Date();
    this.isProgressBarVisble = false; // If progress bar is visible => hide it
    this.processChrtGenBacktestResults(msgObjStr);
  }

  onStartBacktestsClicked() {
    if (this._socket != null && this._socket.readyState === this._socket.OPEN) {
      this.onStartBacktests();
      this._socket.send('RunBacktest:' + '?pids=' + this.prtfIds + '&bmrks=' + this.bmrks); // parameter example can be pids=1,13,6&bmrks=SPY,QQQ&start=20210101&end=20220305
      this.startDate = new Date(this.startDate);
      this.endDate = new Date(this.endDate);
    }
  }

  showProgressBar() {
    const progsBar = document.querySelector('.progressBar') as HTMLElement;
    progsBar.style.animation = ''; // Reset the animation by setting the animation property to an empty string to return to its original state

    this.isProgressBarVisble = true;
    const estimatedDurationInSeconds = gChrtGenDiag.serverBacktestTime / 1000;
    const estimatedDuration = estimatedDurationInSeconds <= 0 ? 4 : estimatedDurationInSeconds; // if estimatedDuration cannot be calculated than, assume 4sec
    console.log('showProgressBar: estimatedDuration', estimatedDuration);
    progsBar.style.animationName = 'progressAnimation';
    progsBar.style.animationDuration = estimatedDuration + 's';
    progsBar.style.animationTimingFunction = 'linear'; // default would be ‘ease’, which is a slow start, then fast, before it ends slowly. We prefer the linear.
    progsBar.style.animationIterationCount = '1'; // only once
    progsBar.style.animationFillMode = 'forwards';
  }

  // "Server backtest time: 300ms, Communication overhead: 120ms, Total UI response: 420ms."
  mouseEnter(div: string) {
    if (div === 'chrtGenDiagnosticsMsg') {
      const totalUiResponseTime = (gChrtGenDiag.backtestRequestReturnTime.getTime() - gChrtGenDiag.backtestRequestStartTime.getTime());
      const communicationOverheadTime = totalUiResponseTime - gChrtGenDiag.serverBacktestTime;
      if (this.isSrvConnectionAlive) {
        this.chrtGenDiagnosticsMsg = `App constructor: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.mainTsTime, gChrtGenDiag.mainAngComponentConstructorTime)}\n` +
        `Window loaded: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.mainTsTime, gChrtGenDiag.windowOnLoadTime)}\n` +
        '-----\n' +
        `Server backtest time: ${gChrtGenDiag.serverBacktestTime + 'ms' }\n`+
        `Total UI response: ${totalUiResponseTime +'ms'}\n` +
        `Communication Overhead: ${communicationOverheadTime +'ms'}\n`;
      } else
        this.chrtGenDiagnosticsMsg = 'Connection to server is broken.\n Try page reload (F5).';
    }
  }

  onHistRangeSelectionClicked(histPeriodSelectionSelected: string) {
    this.histRangeSelected = histPeriodSelectionSelected;
    console.log('hist period selected is ', this.histRangeSelected);
    const currDateET: Date = new Date(); // gets today's date
    if (this.histRangeSelected.toUpperCase() === 'YTD')
      this.startDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.getFullYear() - 1, 11, 31));
    else if (this.histRangeSelected.toLowerCase().endsWith('y')) {
      const lbYears = parseInt(this.histRangeSelected.substr(0, this.histRangeSelected.length - 1), 10);
      this.startDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.setFullYear(currDateET.getFullYear() - lbYears)));
    } else if (this.histRangeSelected.toLowerCase().endsWith('m')) {
      const lbMonths = parseInt(this.histRangeSelected.substr(0, this.histRangeSelected.length - 1), 10);
      this.startDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.setMonth(currDateET.getMonth() - lbMonths)));
    }
    this.endDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date());
    this.startDate = new Date(this.startDateStr);
    this.endDate = new Date(this.endDateStr);
    AppComponent.updateUiWithChrtGenBacktestResults(this.chrtGenBacktestResults, this.uiChrtGenPrtfRunResults, this.pvChrtWidth, this.pvChrtHeight, this.startDate, this.endDate);
  }
}