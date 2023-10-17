import { Component, OnInit, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime, minDate, maxDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { UltimateChart } from '../../../../TsLib/sq-common/chartUltimate';
import { SqStatisticsBuilder, FinalStatistics } from '../../../../TsLib/sq-common/backtestStatistics';
import { ChrtGenBacktestResult, UiChrtGenPrtfRunResult, CgTimeSeries, SqLog, ChartResolution, UiChartPoint, FolderJs, PortfolioJs, prtfsParseHelper, fldrsParseHelper, TreeViewState, TreeViewItem, createTreeViewData, PrtfItemType, LineStyle, ChartJs } from '../../../../TsLib/sq-common/backtestCommon';
import { SqTreeViewComponent } from '../../../sq-ng-common/src/lib/sq-tree-view/sq-tree-view.component';
import { parseNumberToDate } from '../../../../TsLib/sq-common/utils-common';

type Nullable<T> = T | null;

class HandshakeMessage {
  public email = '';
  public anyParam = -1;
  public prtfsToClient: Nullable<PortfolioJs[]> = null;
  public fldrsToClient: Nullable<FolderJs[]> = null;
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
  @ViewChild(SqTreeViewComponent) public _rootTreeComponent!: SqTreeViewComponent; // allows accessing the data from child to parent

  chrtGenBacktestResults: Nullable<ChrtGenBacktestResult> = null;
  uiChrtGenPrtfRunResults: UiChrtGenPrtfRunResult[] = [];
  _minStartDate: Date = maxDate; // recalculated based on the BacktestResult received
  _maxEndDate: Date = minDate;

  _ultimateChrt: UltimateChart = new UltimateChart();
  pvChrtWidth: number = 0;
  pvChrtHeight: number = 0;

  prtfIds: Nullable<string> = null;
  bmrks: Nullable<string> = null; // benchmarks
  startDate: Date = new Date(); // used to filter the chart Data based on the user input
  endDate: Date = new Date(); // used to filter the chart Data based on the user input
  startDateStr: string = '';
  endDateStr: string = '';
  rangeSelection: string[] = ['YTD', '1M', '1Y', '3Y', '5Y', '10Y', 'ALL'];
  histRangeSelected: string = 'ALL';
  isSrvConnectionAlive: boolean = true;
  chrtGenDiagnosticsMsg: string = 'Benchmarking time, connection speed';
  isProgressBarVisble: boolean = false;
  isBacktestReturned: boolean = false;
  _sqStatisticsbuilder: SqStatisticsBuilder = new SqStatisticsBuilder();
  backtestStatsResults: FinalStatistics[] = [];
  _allPortfolios: Nullable<PortfolioJs[]> = null;
  _allFolders: Nullable<FolderJs[]> = null;
  prtfSelectedName: Nullable<string> = null;
  prtfSelectedId: number = 0;
  public gPortfolioIdOffset: number = 10000;
  _backtestedPortfolios: PortfolioJs[] = [];
  _backtestedBenchmarks: string[] = [];
  treeViewState: TreeViewState = new TreeViewState();
  uiNestedPrtfTreeViewItems: TreeViewItem[] = [];
  isSelectPortfoliosFromTreeClicked: boolean = false;

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
    const bmrksTickers: string[] = url.searchParams.get('bmrks')!.trim().split(',');
    for (const item of bmrksTickers) {
      if (!this._backtestedBenchmarks.includes(item)) // check if the item is included or not
        this._backtestedBenchmarks.push(item);
    }

    this.onStartBacktests();
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

          const handshakeMsg: HandshakeMessage = JSON.parse(msgObjStr, function(this: any, key: string, value: any) {
            // eslint-disable-next-line no-invalid-this
            const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used
            const isRemoveOriginalPrtfs: boolean = prtfsParseHelper(_this, key, value);
            if (isRemoveOriginalPrtfs)
              return; // if return undefined, original property will be removed
            const isRemoveOriginalFldrs: boolean = fldrsParseHelper(_this, key, value);
            if (isRemoveOriginalFldrs)
              return; // if return undefined, original property will be removed
            return value; // the original property will not be removed if we return the original value, not undefined
          });
          this.user.email = handshakeMsg.email;
          this._allPortfolios = handshakeMsg.prtfsToClient;
          this._allPortfolios?.forEach((r) => r.prtfItemType = PrtfItemType.Portfolio);
          this._allFolders = handshakeMsg.fldrsToClient;
          this._allFolders?.forEach((r) => r.prtfItemType = PrtfItemType.Folder);
          this.uiNestedPrtfTreeViewItems = createTreeViewData(this._allFolders, this._allPortfolios, this.treeViewState); // process folders and portfolios
          console.log('OnConnected, this.uiNestedPrtfTreeViewItems: ', this.uiNestedPrtfTreeViewItems);
          // Get the Url param of PrtfIds and fill the backtestedPortfolios
          if (this._allPortfolios == null) // it can be null if Handshake message is wrong.
            return;
          const url = new URL(window.location.href);
          const prtfStrIds: string[] = url.searchParams.get('pids')!.trim().split(',');
          for (let i = 0; i < prtfStrIds.length; i++) {
            for (let j = 0; j < this._allPortfolios.length; j++) {
              const id = this._allPortfolios[j].id - this.gPortfolioIdOffset;
              if (id == parseInt(prtfStrIds[i]))
                this._backtestedPortfolios.push(this._allPortfolios[j]);
            }
          }
          break;
        case 'BacktestResults':
          // "await sleep(5000); // simulate slow C# server backtest" - in case we need to Debug something around this in the future.
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
      this._ultimateChrt.Redraw(this.startDate, this.endDate, this.pvChrtWidth, this.pvChrtHeight);
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
    this.updateUiWithChrtGenBacktestResults(this.chrtGenBacktestResults, this.uiChrtGenPrtfRunResults);
  }

  // startdate and enddate are not utlized at the moment - Daya yet to develop
  updateUiWithChrtGenBacktestResults(chrtGenBacktestRes: Nullable<ChrtGenBacktestResult>, uiChrtGenPrtfRunResults: UiChrtGenPrtfRunResult[]) {
    if (chrtGenBacktestRes == null || chrtGenBacktestRes.pfRunResults == null)
      return;

    uiChrtGenPrtfRunResults.length = 0;
    const uiPrtfResItem = new UiChrtGenPrtfRunResult();
    gChrtGenDiag.serverBacktestTime = chrtGenBacktestRes.serverBacktestTimeMs;

    for (const item of chrtGenBacktestRes.pfRunResults) {
      const { firstVal: firstValDate, lastVal: lastValDate } = this.getDateRangeFromChrtData(item.chrtData);
      this.updateMinMaxDates(firstValDate, lastValDate);
      const chartItem = this.createCgTimeSeriesFromChrtData(item.chrtData, item.name, true);
      uiPrtfResItem.prtfChrtValues.push(chartItem);
    }

    for (const bmrkItem of chrtGenBacktestRes.bmrkHistories) {
      const { firstVal: firstValDate, lastVal: lastValDate } = this.getDateRangeFromChrtData(bmrkItem.chrtData);
      this.updateMinMaxDates(firstValDate, lastValDate);
      const chartItem = this.createCgTimeSeriesFromChrtData(bmrkItem.chrtData, bmrkItem.sqTicker, false);
      uiPrtfResItem.bmrkChrtValues.push(chartItem);
    }

    for (const item of chrtGenBacktestRes.logs) {
      const logItem = new SqLog();
      logItem.sqLogLevel = item.sqLogLevel;
      logItem.message = item.message;
      uiPrtfResItem.sqLogs.push(logItem);
    }

    uiChrtGenPrtfRunResults.push(uiPrtfResItem);

    const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
    const prtfAndBmrkChrtData: CgTimeSeries[] = uiChrtGenPrtfRunResults[0].prtfChrtValues.concat(uiChrtGenPrtfRunResults[0].bmrkChrtValues);
    const lineChrtTooltip = document.getElementById('tooltipChart') as HTMLElement;

    this.startDate = this._minStartDate;
    this.endDate = this._maxEndDate;
    this.startDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.startDate);
    this.endDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.endDate);
    this.histRangeSelected = 'ALL';
    this._ultimateChrt.Init(lineChrtDiv, lineChrtTooltip, prtfAndBmrkChrtData);
    this._sqStatisticsbuilder.Init(prtfAndBmrkChrtData);
    this.onStartOrEndDateChanged(); // will recalculate CAGR and redraw chart
  }

  // Common function for both portfolios and bmrks to create chartGenerator TimeSeries data
  createCgTimeSeriesFromChrtData(chrtData: ChartJs, name: string, isPrimary: boolean): CgTimeSeries {
    const chartItem = new CgTimeSeries();
    chartItem.name = name;
    chartItem.chartResolution = ChartResolution[chrtData.chartResolution];
    chartItem.linestyle = isPrimary ? LineStyle.Solid : LineStyle.Dashed;
    chartItem.isPrimary = isPrimary;
    chartItem.priceData = [];

    for (let i = 0; i < chrtData.dates.length; i++) {
      const chrtItem = this.createUiChartPointFromChrtData(chrtData, i);
      chartItem.priceData.push(chrtItem);
    }
    return chartItem;
  }

  // Common function for both portfolios and bmrks to create UiChartPiont data from chartdata and index
  createUiChartPointFromChrtData(chrtData: ChartJs, index: number): UiChartPoint {
    const chrtItem = new UiChartPoint();

    if (chrtData.dateTimeFormat == 'YYYYMMDD')
      chrtItem.date = parseNumberToDate(chrtData.dates[index]);
    else if (chrtData.dateTimeFormat.includes('DaysFrom')) {
      const semicolonInd = chrtData.dateTimeFormat.indexOf(':');
      const dateStartsFrom = parseNumberToDate(parseInt(chrtData.dateTimeFormat.substring(semicolonInd + 1)));
      chrtItem.date = new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + chrtData.dates[index]));
    } else {
      const mSecSinceUnixEpoch: number = chrtData.dates[index] * 1000;
      chrtItem.date = new Date(mSecSinceUnixEpoch);
    }
    chrtItem.value = chrtData.values[index];
    return chrtItem;
  }

  // Common function for both portfolios and bmrks to DateRanges from chartData.
  getDateRangeFromChrtData(chrtData: ChartJs): { firstVal: Date, lastVal: Date } {
    let firstValDate: Date;
    let lastValDate: Date;

    if (chrtData.dateTimeFormat == 'YYYYMMDD') {
      firstValDate = parseNumberToDate(chrtData.dates[0]);
      lastValDate = parseNumberToDate(chrtData.dates[chrtData.dates.length - 1]);
    } else if (chrtData.dateTimeFormat.includes('DaysFrom')) {
      const semicolonInd = chrtData.dateTimeFormat.indexOf(':');
      const dateStartsFrom = parseNumberToDate(parseInt(chrtData.dateTimeFormat.substring(semicolonInd + 1)));
      firstValDate = new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + chrtData.dates[0]));
      lastValDate = new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + chrtData.dates[chrtData.dates.length - 1]));
    } else {
      firstValDate = new Date(chrtData.dates[0] * 1000);
      lastValDate = new Date(chrtData.dates[chrtData.dates.length - 1] * 1000);
    }
    return { firstVal: firstValDate, lastVal: lastValDate };
  }

  // Based on the DateRanges both portfolios and bmrks update the min and max dates.
  updateMinMaxDates(minDate: Date, maxDate: Date) {
    if (minDate < this._minStartDate)
      this._minStartDate = minDate;

    if (maxDate > this._maxEndDate)
      this._maxEndDate = maxDate;
  }

  onStartOrEndDateChanged() {
    // Recalculate the totalReturn and CAGR here
    this.backtestStatsResults = this._sqStatisticsbuilder.statsResults(this.startDate, this.endDate);
    console.log('onStartOrEndDateChanged: this._sqStatisticsbuilder', this.backtestStatsResults.length);
    this._ultimateChrt.Redraw(this.startDate, this.endDate, this.pvChrtWidth, this.pvChrtHeight);
  }

  async onStartBacktests() {
    this.isBacktestReturned = false;
    gChrtGenDiag.backtestRequestStartTime = new Date();
    // Remember to Show Progress bar in 2 seconds from this time.
    setTimeout(() => {
      if (!this.isBacktestReturned) // If the backtest hasn't returned yet (still pending), show Progress bar
        this.showProgressBar();
    }, 2 * 1000);
  }

  onCompleteBacktests(msgObjStr: string) {
    this.startDate = new Date(this.startDate);
    this.endDate = new Date(this.endDate);
    // Whenever server backtest starts - resetting the _minStartDate and _maxEndDate
    this._minStartDate = maxDate;
    this._maxEndDate = minDate;
    this.isBacktestReturned = true;
    gChrtGenDiag.backtestRequestReturnTime = new Date();
    this.isProgressBarVisble = false; // If progress bar is visible => hide it
    this.processChrtGenBacktestResults(msgObjStr);
  }

  onStartBacktestsClicked() {
    if (this._socket != null && this._socket.readyState == this._socket.OPEN) {
      this.prtfIds = ''; // empty the prtfIds
      for (const item of this._backtestedPortfolios) // iterate to add the backtested portfolioIds selected by the user
        this.prtfIds += item.id - this.gPortfolioIdOffset + ',';
      this.bmrks = this._backtestedBenchmarks.join(',');
      this.onStartBacktests();
      this._socket.send('RunBacktest:' + '?pids=' + this.prtfIds + '&bmrks=' + this.bmrks); // parameter example can be pids=1,13,6&bmrks=SPY,QQQ&start=20210101&end=20220305
    }
    this.bmrks = ''; // we need to immediatley make it empty else there will be a blink of a bmrk value in the input box.
    console.log('the prtfIds length is:', this.prtfIds);
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

  onUserChangedHistDateRange(histPeriodSelectionSelected: string) { // selection made form the list ['YTD', '1M', '1Y', '3Y', '5Y', 'ALL']
    this.histRangeSelected = histPeriodSelectionSelected;
    const currDateET: Date = new Date(); // gets today's date
    if (this.histRangeSelected === 'YTD')
      this.startDate = new Date(SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.getFullYear() - 1, 11, 31)));
    else if (this.histRangeSelected.toLowerCase().endsWith('y')) {
      const lbYears = parseInt(this.histRangeSelected.substr(0, this.histRangeSelected.length - 1), 10);
      this.startDate = new Date(SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.setFullYear(currDateET.getFullYear() - lbYears))));
    } else if (this.histRangeSelected.toLowerCase().endsWith('m')) {
      const lbMonths = parseInt(this.histRangeSelected.substr(0, this.histRangeSelected.length - 1), 10);
      this.startDate = new Date(SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.setMonth(currDateET.getMonth() - lbMonths))));
    } else if (this.histRangeSelected === 'ALL')
      this.startDate = this._minStartDate;
    this.startDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.startDate);
    this.endDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this._maxEndDate); // Interestingly, when we change this which is bind to the date input html element, then the onChangeStartOrEndDate() is not called.
    this.endDate = this._maxEndDate;
    this.onStartOrEndDateChanged();
  }

  onUserChangedStartOrEndDateWidgets() { // User entry in the input field
    this.startDate = new Date(this.startDateStr);
    this.endDate = new Date(this.endDateStr);
    this.onStartOrEndDateChanged();
  }

  onClickUserSelectedPortfolio(prtf: PortfolioJs) {
    this.prtfSelectedName = prtf.name;
    this.prtfSelectedId = prtf.id;
  }

  onClickPrtfSelectedForBacktest(prtfSelectedId: number) {
    if (this._allPortfolios == null)
      return;
    const prtfId = prtfSelectedId - this.gPortfolioIdOffset; // remove the offset from the prtfSelectedId to get the proper Id from Db
    let prtfSelectedInd = -1;
    for (let i = 0; i < this._backtestedPortfolios.length; i++) {
      if (this._backtestedPortfolios[i].id == prtfId) {
        prtfSelectedInd = i; // get the index, if the item is found
        break;
      }
    }

    // If the item is not already included, proceed to add it
    if (prtfSelectedInd == -1) {
      const allPortfoliosInd = this._allPortfolios.findIndex((item) => item.id == prtfSelectedId); // Find the index of the selected item in _allPortfolios
      if (allPortfoliosInd != -1 && !this._backtestedPortfolios.includes(this._allPortfolios[allPortfoliosInd])) // check if the item is included or not
        this._backtestedPortfolios.push(this._allPortfolios[allPortfoliosInd]); // Push the selected item from _allPortfolios into _backtestedPortfolios
    }
    this.prtfSelectedName = ''; // clearing the textbox after inserting the prtf.
  }

  onClickClearBacktestedPortfolios() { // clear the user selected backtested portfolios
    this._backtestedPortfolios.length = 0;
  }

  onClickBmrkSelectedForBacktest() {
    const bmrkArray: string[] = this.bmrks!.trim().split(',');
    for (const item of bmrkArray) {
      if (!this._backtestedBenchmarks.includes(item)) // check if the item is included or not
        this._backtestedBenchmarks.push(item);
    }
    this.bmrks = ''; // clearing the textbox after inserting the bmrks.
  }

  onClickClearBacktestedBnmrks() { // clear the user selected backtested Benchmarks
    this._backtestedBenchmarks.length = 0;
  }

  onClickSelectFromTree() {
    this.isSelectPortfoliosFromTreeClicked = !this.isSelectPortfoliosFromTreeClicked;
  }

  onClickPrtfSelectedFromTreeForBacktest() { // This code is similar to the method onClickPrtfSelectedForBacktest(). Once we finalize the SelecFromTree option we can remove onClickPrtfSelectedForBacktest().
    const lastSelectedTreeNode = this.treeViewState.lastSelectedItem;
    if (lastSelectedTreeNode == null || lastSelectedTreeNode?.prtfItemType != 'Portfolio')
      return;
    const prtfId = lastSelectedTreeNode.id - this.gPortfolioIdOffset; // remove the offset from the prtfSelectedId to get the proper Id from Db
    let prtfSelectedInd = -1;
    for (let i = 0; i < this._backtestedPortfolios.length; i++) {
      if (this._backtestedPortfolios[i].id == prtfId) {
        prtfSelectedInd = i; // get the index, if the item is found
        break;
      }
    }

    // If the item is not already included, proceed to add it
    if (prtfSelectedInd == -1) {
      const allPortfoliosInd = this._allPortfolios?.findIndex((item) => item.id == lastSelectedTreeNode.id); // Find the index of the selected item in _allPortfolios
      if (allPortfoliosInd != -1 && !this._backtestedPortfolios.includes(this._allPortfolios![allPortfoliosInd!])) // check if the item is included or not
        this._backtestedPortfolios.push(this._allPortfolios![allPortfoliosInd!]); // Push the selected item from _allPortfolios into _backtestedPortfolios
    }
  }
}