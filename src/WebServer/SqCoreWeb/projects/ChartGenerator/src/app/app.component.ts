import { Component, OnInit, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime, minDate, maxDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { UltimateChart } from '../../../../TsLib/sq-common/chartUltimate';
import { SqStatisticsBuilder, FinalStatistics } from '../../../../TsLib/sq-common/backtestStatistics';
import { ChrtGenBacktestResult, UiChrtGenPrtfRunResult, CgTimeSeries, SqLog, ChartResolution, UiChartPoint, FolderJs, PortfolioJs, prtfsParseHelper, fldrsParseHelper, TreeViewState, TreeViewItem, createTreeViewData, PrtfItemType, LineStyle, ChartJs, MonthlySeasonality, SeasonalityData } from '../../../../TsLib/sq-common/backtestCommon';
import { SqTreeViewComponent } from '../../../sq-ng-common/src/lib/sq-tree-view/sq-tree-view.component';
import { parseNumberToDate } from '../../../../TsLib/sq-common/utils-common';
import { sqAverageOfSeasonalityData, sqMedian } from '../../../../TsLib/sq-common/utils_math';

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
  public static readonly cSecToMSec: number = 1000;
  _backtestedPortfolios: PortfolioJs[] = [];
  _backtestedBenchmarks: string[] = [];
  treeViewState: TreeViewState = new TreeViewState();
  uiNestedPrtfTreeViewItems: TreeViewItem[] = [];
  isPrtfSelectionDialogVisible: boolean = false;
  m_seasonalityData: SeasonalityData[] = [];

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
      this.m_seasonalityData.push(this.getSeasonalityData(item.chrtData));
      uiPrtfResItem.prtfChrtValues.push(chartItem);
    }

    for (const bmrkItem of chrtGenBacktestRes.bmrkHistories) {
      const { firstVal: firstValDate, lastVal: lastValDate } = this.getDateRangeFromChrtData(bmrkItem.chrtData);
      this.updateMinMaxDates(firstValDate, lastValDate);
      const chartItem = this.createCgTimeSeriesFromChrtData(bmrkItem.chrtData, bmrkItem.sqTicker, false);
      this.m_seasonalityData.push(this.getSeasonalityData(bmrkItem.chrtData));
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
      const dateStartInd = chrtData.dateTimeFormat.indexOf('m');
      const dateStartsFrom = parseNumberToDate(parseInt(chrtData.dateTimeFormat.substring(dateStartInd + 1)));
      chrtItem.date = new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + chrtData.dates[index]));
    } else
      chrtItem.date = new Date(chrtData.dates[index] * AppComponent.cSecToMSec); // data comes as seconds. JS uses milliseconds since Epoch.
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
      const dateStartInd = chrtData.dateTimeFormat.indexOf('m');
      const dateStartsFrom = parseNumberToDate(parseInt(chrtData.dateTimeFormat.substring(dateStartInd + 1)));
      firstValDate = new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + chrtData.dates[0]));
      lastValDate = new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + chrtData.dates[chrtData.dates.length - 1]));
    } else {
      firstValDate = new Date(chrtData.dates[0] * AppComponent.cSecToMSec); // data comes as seconds. JS uses milliseconds since Epoch.
      lastValDate = new Date(chrtData.dates[chrtData.dates.length - 1] * AppComponent.cSecToMSec);
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
    this.chrtGenBacktestResults = JSON.parse(msgObjStr);
    this.updateUiWithChrtGenBacktestResults(this.chrtGenBacktestResults, this.uiChrtGenPrtfRunResults);
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
    this.isPrtfSelectionDialogVisible = !this.isPrtfSelectionDialogVisible;
  }

  onClickPrtfSelectedFromTreeView() {
    // Collect checked items locally
    const checkedItems: TreeViewItem[] = [];
    this.collectCheckedItemsAndAllChildren(this.uiNestedPrtfTreeViewItems, checkedItems);

    for (const checkedItem of checkedItems) {
      const portfolioItem = this._allPortfolios!.find((item) => item.id == checkedItem.id);
      if (portfolioItem != null && !this._backtestedPortfolios.includes(portfolioItem))
        this._backtestedPortfolios.push(portfolioItem);
    }

    // Reset PrtfTreeviewItems state
    for (const item of this.uiNestedPrtfTreeViewItems)
      this.resetThisItemAndAllChildren(item);

    this.isPrtfSelectionDialogVisible = false;
  }

  collectCheckedItemsAndAllChildren(prtfTreeViewItems: TreeViewItem[], checkedItems: TreeViewItem[]) {
    for (const item of prtfTreeViewItems) {
      // Check if isCheckboxChecked is truthy, ex: isCheckboxChecked == true.
      if (item.isCheckboxChecked)
        checkedItems.push(item);

      if (item.children != null && item.children.length > 0)
        this.collectCheckedItemsAndAllChildren(item.children, checkedItems);
    }
  }

  resetThisItemAndAllChildren(item: TreeViewItem) { // resetting the treeview items back to its original state. Otherwise the treeview state will remain expanded, checked and selected if the user has done any of this.
    item.isExpanded = false;
    item.isCheckboxChecked = false;
    item.isSelected = false;

    if (item.children != null && item.children.length > 0) {
      for (const child of item.children)
        this.resetThisItemAndAllChildren(child);
    }
  }

  getSeasonalityData(chartData: ChartJs): SeasonalityData {
    const histData: ChartJs = chartData;
    const seasonalityData = new SeasonalityData();
    if (histData == null)
      return seasonalityData;
    // step1: group the data by year and month and get the last value of each month
    // step2: calculate the monthly percentage return
    // step2.1: example: yearMonth: 201012, value: 8.58, yearMonth: 201101, value: 13.04
    // step2.2: return: 201101: 13.04/8.58 = 0.519
    // step3: now store the year and return value in MonthlySeasonality: Object
    // ex: seasonality: monthlySeasonality = [ { year:2011, returns:[0.519, 0.62, 0.75, ......] },]

    // Step1: Group the data into respective months and assign the last value of each month directly
    const groupedMonthlyReturn: { [key: string]: number } = {};
    let date: string = '';
    for (let i = 0; i < histData.dates.length; i++) {
      if (histData.dateTimeFormat == 'YYYYMMDD')
        date = SqNgCommonUtilsTime.Date2PaddedIsoStr(parseNumberToDate(histData.dates[i]));
      else if (histData.dateTimeFormat.includes('DaysFrom')) {
        const dateStartInd = histData.dateTimeFormat.indexOf('m');
        const dateStartsFrom = parseNumberToDate(parseInt(histData.dateTimeFormat.substring(dateStartInd + 1)));
        date = SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + histData.dates[i])));
      } else
        date = SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(histData.dates[i] * AppComponent.cSecToMSec)); // data comes as seconds. JS uses milliseconds since Epoch.
      const value = histData.values[i]; // Get the corresponding value
      const [year, month] = date.split('-'); // Extract year and month from the ISO string
      const yearMonth: string = `${year}-${month}`; // Create the 'year-month' key

      groupedMonthlyReturn[yearMonth] = value; // Assign the last value encountered for this month
    }

    const isGroupedMonthlyReturnHasAtleast2DataPoints: boolean = Object.keys(groupedMonthlyReturn).length >= 2; // Ensure there are at least 2 months of data to avoid crashes when only 1 month of data is available. Example: PortfolioId: 1, Name: Test-NoUserRootPortfolio
    if (isGroupedMonthlyReturnHasAtleast2DataPoints) {
    // Step2: Monthly percentage return calculation
      const monthlyPercentageReturn: { [key: string]: number } = {};
      const yearMonthKeys: string[] = Object.keys(groupedMonthlyReturn); // e.g. keys: [2024-01, 2024-02 ...]
      for (let i = 0; i < yearMonthKeys.length; i++) {
        const currentMonth: string = yearMonthKeys[i];
        const previousMonth: string = yearMonthKeys[i - 1];
        monthlyPercentageReturn[currentMonth] = (groupedMonthlyReturn[currentMonth] - groupedMonthlyReturn[previousMonth]) / groupedMonthlyReturn[previousMonth]; // Calculate Percentage Change: For each new month, calculate the percentage change using the last stored value from the previous month.
      }

      // Step3: Group the data by year wise
      const monthlySeasonality: MonthlySeasonality[] = [];
      // Iterate over each key-value pair in monthlyPercentageReturn to populate the monthly seasonality data
      for (const [yearMonth, value] of Object.entries(monthlyPercentageReturn).reverse()) { // reverse() - is used to show the latest data on top in the matrix on UI
        const [year, month] = yearMonth.split('-'); // Extract year and month
        const monthIndex: number = parseInt(month, 10) - 1; // Convert month to zero-based index (0 for January, 11 for December)

        // Check if the seasonality data for the current year already exists
        let existingSeasonality: MonthlySeasonality | undefined;
        for (let i = 0; i < monthlySeasonality.length; i++) {
          if (monthlySeasonality[i].year == year) {
            existingSeasonality = monthlySeasonality[i];
            break;
          }
        }

        // If no existing seasonality data is found for the current year, create a new one
        if (existingSeasonality == undefined) {
          existingSeasonality = new MonthlySeasonality();
          existingSeasonality.year = year;
          existingSeasonality.returns = new Array(12); // Initialize an empty array with 12 elements for each month of the year
          monthlySeasonality.push(existingSeasonality);
        }

        existingSeasonality.returns[monthIndex] = value;
      }

      seasonalityData.monthlySeasonality = monthlySeasonality;
      // this.m_seasonalityData.push(seasonality);
      console.log('extractMonthlySeasonality: monthlySeasonality', this.m_seasonalityData.length);

      // Winrate Calculation
      const positiveMonthlyReturnsCount: number[] = new Array(12).fill(0);
      const negativeMonthlyReturnsCount: number[] = new Array(12).fill(0);

      // Iterate over the monthly seasonality data
      for (const mnthSeasonlity of monthlySeasonality) {
        for (let i = 0; i < mnthSeasonlity.returns.length; i++) {
          if (mnthSeasonlity.returns[i] > 0) // If the return is greater than zero, increment the count of positive monthly returns.
            positiveMonthlyReturnsCount[i]++;
          else if (mnthSeasonlity.returns[i] < 0) // If the return is less than zero, increment the count of negative monthly returns.
            negativeMonthlyReturnsCount[i]++;
        }
      }

      for (let i = 0; i < 12; i++) // Calculate the win rate for each month
        seasonalityData.monthlySeasonalityWinrate[i] = positiveMonthlyReturnsCount[i] / (positiveMonthlyReturnsCount[i] + negativeMonthlyReturnsCount[i]);

      const numOfYears: number = monthlySeasonality.length; // Represents the number of years of data available, used to ensure sufficient data for calculating 3, 5, and 10-year averages, preventing potential crashes.
      seasonalityData.monthlySeasonality3yAvg = numOfYears >= 3 ? sqAverageOfSeasonalityData(monthlySeasonality, 3) : seasonalityData.monthlySeasonality3yAvg; // Calculate 3-year average
      seasonalityData.monthlySeasonality5yAvg = numOfYears >= 5 ? sqAverageOfSeasonalityData(monthlySeasonality, 5) : seasonalityData.monthlySeasonality5yAvg; // Calculate 5-year average
      seasonalityData.monthlySeasonality10yAvg = numOfYears >= 10 ? sqAverageOfSeasonalityData(monthlySeasonality, 10) : seasonalityData.monthlySeasonality10yAvg; // Calculate 10-year average
      seasonalityData.monthlySeasonalityAvg = sqAverageOfSeasonalityData(monthlySeasonality, numOfYears); // Calculate overall average (mean) for all available data

      // Median calculation
      for (let i = 0; i < 12; i++) {
        const returns: number[] = [];
        for (let j = 0; j < monthlySeasonality.length; j++) {
          if (monthlySeasonality[j].returns[i] != undefined && !Number.isNaN(monthlySeasonality[j].returns[i]))
            returns.push(monthlySeasonality[j].returns[i]);
        }
        seasonalityData.monthlySeasonalityMedian[i] = sqMedian(returns);
      }
    }
    return seasonalityData;
  }
}