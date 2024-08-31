import { Component, OnInit, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime, minDate, maxDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { UltimateChart } from '../../../../TsLib/sq-common/chartUltimate';
import { SqStatisticsBuilder, FinalStatistics } from '../../../../TsLib/sq-common/backtestStatistics';
import { ChrtGenBacktestResult, UiChrtGenPrtfRunResult, CgTimeSeries, SqLog, ChartResolution, UiChartPoint, FolderJs, PortfolioJs, prtfsParseHelper, fldrsParseHelper, TreeViewState, TreeViewItem, createTreeViewData, PrtfItemType, LineStyle, ChartJs, SeasonalityData, getSeasonalityData } from '../../../../TsLib/sq-common/backtestCommon';
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

  // Portfolios & BenchMark Sections
  m_prtfIds: Nullable<string> = null;
  m_treeViewState: TreeViewState = new TreeViewState();
  m_uiNestedPrtfTreeViewItems: TreeViewItem[] = [];
  m_allPortfolios: Nullable<PortfolioJs[]> = null;
  m_allFolders: Nullable<FolderJs[]> = null;
  m_prtfSelectedName: Nullable<string> = null;
  m_prtfSelectedId: number = 0;
  m_bmrks: Nullable<string> = null; // benchmarks
  m_sqStatisticsbuilder: SqStatisticsBuilder = new SqStatisticsBuilder();
  m_backtestStatsResults: FinalStatistics[] = [];
  m_backtestedPortfolios: PortfolioJs[] = [];
  m_backtestedBenchmarks: string[] = [];

  // BacktestResults and charts sections
  m_chrtGenBacktestResults: Nullable<ChrtGenBacktestResult> = null;
  m_uiChrtGenPrtfRunResults: UiChrtGenPrtfRunResult[] = [];
  m_minStartDate: Date = maxDate; // recalculated based on the BacktestResult received
  m_maxEndDate: Date = minDate;
  m_ultimateChrt: UltimateChart = new UltimateChart();
  m_pvChrtWidth: number = 0;
  m_pvChrtHeight: number = 0;

  // Historical range selection
  m_startDate: Date = new Date(); // used to filter the chart Data based on the user input
  m_endDate: Date = new Date(); // used to filter the chart Data based on the user input
  m_startDateStr: string = '';
  m_endDateStr: string = '';
  m_rangeSelection: string[] = ['YTD', '1M', '1Y', '3Y', '5Y', '10Y', 'ALL'];
  m_histRangeSelected: string = 'ALL';

  m_isSrvConnectionAlive: boolean = true;
  m_chrtGenDiagnosticsMsg: string = 'Benchmarking time, connection speed';
  m_isProgressBarVisble: boolean = false;
  m_isBacktestReturned: boolean = false;
  m_isPrtfSelectionDialogVisible: boolean = false;

  m_seasonalityData: SeasonalityData[] = []; // Seasonality

  // Constants
  public gPortfolioIdOffset: number = 10000;
  public static readonly cSecToMSec: number = 1000;

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
      if (!this.m_backtestedBenchmarks.includes(item)) // check if the item is included or not
        this.m_backtestedBenchmarks.push(item);
    }

    this.onStartBacktests();
    this._socket = new WebSocket('wss://' + document.location.hostname + '/ws/chrtgen' + wsQueryStr); // "wss://127.0.0.1/ws/chrtgen?pids=13,2" without port number, so it goes directly to port 443, avoiding Angular Proxy redirection. ? has to be included to separate the location from the params

    setInterval(() => { // checking whether the connection is live or not
      this.m_isSrvConnectionAlive = this._socket != null && this._socket.readyState === WebSocket.OPEN;
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
          this.m_allPortfolios = handshakeMsg.prtfsToClient;
          this.m_allPortfolios?.forEach((r) => r.prtfItemType = PrtfItemType.Portfolio);
          this.m_allFolders = handshakeMsg.fldrsToClient;
          this.m_allFolders?.forEach((r) => r.prtfItemType = PrtfItemType.Folder);
          this.m_uiNestedPrtfTreeViewItems = createTreeViewData(this.m_allFolders, this.m_allPortfolios, this.m_treeViewState); // process folders and portfolios
          console.log('OnConnected, this.uiNestedPrtfTreeViewItems: ', this.m_uiNestedPrtfTreeViewItems);
          // Get the Url param of PrtfIds and fill the backtestedPortfolios
          if (this.m_allPortfolios == null) // it can be null if Handshake message is wrong.
            return;
          const url = new URL(window.location.href);
          const prtfStrIds: string[] = url.searchParams.get('pids')!.trim().split(',');
          for (let i = 0; i < prtfStrIds.length; i++) {
            for (let j = 0; j < this.m_allPortfolios.length; j++) {
              const id = this.m_allPortfolios[j].id - this.gPortfolioIdOffset;
              if (id == parseInt(prtfStrIds[i]))
                this.m_backtestedPortfolios.push(this.m_allPortfolios[j]);
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
    this.m_pvChrtWidth = backtestResChartId.clientWidth as number;
    this.m_pvChrtHeight = backtestResChartId.clientHeight as number;
    // resizing the chart dynamically when the window is resized
    window.addEventListener('resize', () => {
      this.m_pvChrtWidth = backtestResChartId.clientWidth as number; // we have to remember the width/height every time window is resized, because we give these to the chart
      this.m_pvChrtHeight = backtestResChartId.clientHeight as number;
      this.m_ultimateChrt.Redraw(this.m_startDate, this.m_endDate, this.m_pvChrtWidth, this.m_pvChrtHeight);
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
      this.m_seasonalityData.push(getSeasonalityData(item.chrtData));
      uiPrtfResItem.prtfChrtValues.push(chartItem);
    }

    for (const bmrkItem of chrtGenBacktestRes.bmrkHistories) {
      const { firstVal: firstValDate, lastVal: lastValDate } = this.getDateRangeFromChrtData(bmrkItem.chrtData);
      this.updateMinMaxDates(firstValDate, lastValDate);
      const chartItem = this.createCgTimeSeriesFromChrtData(bmrkItem.chrtData, bmrkItem.sqTicker, false);
      this.m_seasonalityData.push(getSeasonalityData(bmrkItem.chrtData));
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

    this.m_startDate = this.m_minStartDate;
    this.m_endDate = this.m_maxEndDate;
    this.m_startDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_startDate);
    this.m_endDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_endDate);
    this.m_histRangeSelected = 'ALL';
    this.m_ultimateChrt.Init(lineChrtDiv, lineChrtTooltip, prtfAndBmrkChrtData);
    this.m_sqStatisticsbuilder.Init(prtfAndBmrkChrtData);
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
    if (minDate < this.m_minStartDate)
      this.m_minStartDate = minDate;

    if (maxDate > this.m_maxEndDate)
      this.m_maxEndDate = maxDate;
  }

  onStartOrEndDateChanged() {
    // Recalculate the totalReturn and CAGR here
    this.m_backtestStatsResults = this.m_sqStatisticsbuilder.statsResults(this.m_startDate, this.m_endDate);
    console.log('onStartOrEndDateChanged: this._sqStatisticsbuilder', this.m_backtestStatsResults.length);
    this.m_ultimateChrt.Redraw(this.m_startDate, this.m_endDate, this.m_pvChrtWidth, this.m_pvChrtHeight);
  }

  async onStartBacktests() {
    this.m_isBacktestReturned = false;
    gChrtGenDiag.backtestRequestStartTime = new Date();
    // Remember to Show Progress bar in 2 seconds from this time.
    setTimeout(() => {
      if (!this.m_isBacktestReturned) // If the backtest hasn't returned yet (still pending), show Progress bar
        this.showProgressBar();
    }, 2 * 1000);
  }

  onCompleteBacktests(msgObjStr: string) {
    this.m_startDate = new Date(this.m_startDate);
    this.m_endDate = new Date(this.m_endDate);
    // Whenever server backtest starts - resetting the _minStartDate and _maxEndDate
    this.m_minStartDate = maxDate;
    this.m_maxEndDate = minDate;
    this.m_isBacktestReturned = true;
    gChrtGenDiag.backtestRequestReturnTime = new Date();
    this.m_isProgressBarVisble = false; // If progress bar is visible => hide it
    this.m_chrtGenBacktestResults = JSON.parse(msgObjStr);
    this.updateUiWithChrtGenBacktestResults(this.m_chrtGenBacktestResults, this.m_uiChrtGenPrtfRunResults);
  }

  onStartBacktestsClicked() {
    if (this._socket != null && this._socket.readyState == this._socket.OPEN) {
      this.m_prtfIds = ''; // empty the prtfIds
      for (const item of this.m_backtestedPortfolios) // iterate to add the backtested portfolioIds selected by the user
        this.m_prtfIds += item.id - this.gPortfolioIdOffset + ',';
      this.m_bmrks = this.m_backtestedBenchmarks.join(',');
      this.onStartBacktests();
      this._socket.send('RunBacktest:' + '?pids=' + this.m_prtfIds + '&bmrks=' + this.m_bmrks); // parameter example can be pids=1,13,6&bmrks=SPY,QQQ&start=20210101&end=20220305
    }
    this.m_bmrks = ''; // we need to immediatley make it empty else there will be a blink of a bmrk value in the input box.
    console.log('the prtfIds length is:', this.m_prtfIds);
  }

  showProgressBar() {
    const progsBar = document.querySelector('.progressBar') as HTMLElement;
    progsBar.style.animation = ''; // Reset the animation by setting the animation property to an empty string to return to its original state

    this.m_isProgressBarVisble = true;
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
      if (this.m_isSrvConnectionAlive) {
        this.m_chrtGenDiagnosticsMsg = `App constructor: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.mainTsTime, gChrtGenDiag.mainAngComponentConstructorTime)}\n` +
        `Window loaded: ${SqNgCommonUtilsTime.getTimespanStr(gChrtGenDiag.mainTsTime, gChrtGenDiag.windowOnLoadTime)}\n` +
        '-----\n' +
        `Server backtest time: ${gChrtGenDiag.serverBacktestTime + 'ms' }\n`+
        `Total UI response: ${totalUiResponseTime +'ms'}\n` +
        `Communication Overhead: ${communicationOverheadTime +'ms'}\n`;
      } else
        this.m_chrtGenDiagnosticsMsg = 'Connection to server is broken.\n Try page reload (F5).';
    }
  }

  onUserChangedHistDateRange(histPeriodSelectionSelected: string) { // selection made form the list ['YTD', '1M', '1Y', '3Y', '5Y', 'ALL']
    this.m_histRangeSelected = histPeriodSelectionSelected;
    const currDateET: Date = new Date(); // gets today's date
    if (this.m_histRangeSelected === 'YTD')
      this.m_startDate = new Date(SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.getFullYear() - 1, 11, 31)));
    else if (this.m_histRangeSelected.toLowerCase().endsWith('y')) {
      const lbYears = parseInt(this.m_histRangeSelected.substr(0, this.m_histRangeSelected.length - 1), 10);
      this.m_startDate = new Date(SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.setFullYear(currDateET.getFullYear() - lbYears))));
    } else if (this.m_histRangeSelected.toLowerCase().endsWith('m')) {
      const lbMonths = parseInt(this.m_histRangeSelected.substr(0, this.m_histRangeSelected.length - 1), 10);
      this.m_startDate = new Date(SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(currDateET.setMonth(currDateET.getMonth() - lbMonths))));
    } else if (this.m_histRangeSelected === 'ALL')
      this.m_startDate = this.m_minStartDate;
    this.m_startDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_startDate);
    this.m_endDateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_maxEndDate); // Interestingly, when we change this which is bind to the date input html element, then the onChangeStartOrEndDate() is not called.
    this.m_endDate = this.m_maxEndDate;
    this.onStartOrEndDateChanged();
  }

  onUserChangedStartOrEndDateWidgets() { // User entry in the input field
    this.m_startDate = new Date(this.m_startDateStr);
    this.m_endDate = new Date(this.m_endDateStr);
    this.onStartOrEndDateChanged();
  }

  onClickUserSelectedPortfolio(prtf: PortfolioJs) {
    this.m_prtfSelectedName = prtf.name;
    this.m_prtfSelectedId = prtf.id;
  }

  onClickPrtfSelectedForBacktest(prtfSelectedId: number) {
    if (this.m_allPortfolios == null)
      return;
    const prtfId = prtfSelectedId - this.gPortfolioIdOffset; // remove the offset from the prtfSelectedId to get the proper Id from Db
    let prtfSelectedInd = -1;
    for (let i = 0; i < this.m_backtestedPortfolios.length; i++) {
      if (this.m_backtestedPortfolios[i].id == prtfId) {
        prtfSelectedInd = i; // get the index, if the item is found
        break;
      }
    }

    // If the item is not already included, proceed to add it
    if (prtfSelectedInd == -1) {
      const allPortfoliosInd = this.m_allPortfolios.findIndex((item) => item.id == prtfSelectedId); // Find the index of the selected item in _allPortfolios
      if (allPortfoliosInd != -1 && !this.m_backtestedPortfolios.includes(this.m_allPortfolios[allPortfoliosInd])) // check if the item is included or not
        this.m_backtestedPortfolios.push(this.m_allPortfolios[allPortfoliosInd]); // Push the selected item from _allPortfolios into _backtestedPortfolios
    }
    this.m_prtfSelectedName = ''; // clearing the textbox after inserting the prtf.
  }

  onClickClearBacktestedPortfolios() { // clear the user selected backtested portfolios
    this.m_backtestedPortfolios.length = 0;
  }

  onClickBmrkSelectedForBacktest() {
    const bmrkArray: string[] = this.m_bmrks!.trim().split(',');
    for (const item of bmrkArray) {
      if (!this.m_backtestedBenchmarks.includes(item)) // check if the item is included or not
        this.m_backtestedBenchmarks.push(item);
    }
    this.m_bmrks = ''; // clearing the textbox after inserting the bmrks.
  }

  onClickClearBacktestedBnmrks() { // clear the user selected backtested Benchmarks
    this.m_backtestedBenchmarks.length = 0;
  }

  onClickSelectFromTree() {
    this.m_isPrtfSelectionDialogVisible = !this.m_isPrtfSelectionDialogVisible;
  }

  onClickPrtfSelectedFromTreeView() {
    // Collect checked items locally
    const checkedItems: TreeViewItem[] = [];
    this.collectCheckedItemsAndAllChildren(this.m_uiNestedPrtfTreeViewItems, checkedItems);

    for (const checkedItem of checkedItems) {
      const portfolioItem = this.m_allPortfolios!.find((item) => item.id == checkedItem.id);
      if (portfolioItem != null && !this.m_backtestedPortfolios.includes(portfolioItem))
        this.m_backtestedPortfolios.push(portfolioItem);
    }

    // Reset PrtfTreeviewItems state
    for (const item of this.m_uiNestedPrtfTreeViewItems)
      this.resetThisItemAndAllChildren(item);

    this.m_isPrtfSelectionDialogVisible = false;
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
}