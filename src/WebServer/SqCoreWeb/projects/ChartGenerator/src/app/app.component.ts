import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { SqNgCommonUtils } from './../../../sq-ng-common/src/lib/sq-ng-common.utils';
import { SqNgCommonUtilsTime, minDate, maxDate } from './../../../sq-ng-common/src/lib/sq-ng-common.utils_time';
import { UltimateChart } from '../../../../TsLib/sq-common/chartUltimate';
import { SqStatisticsBuilder, StatisticsResults, DetailedStatistics, BacktestDetailedStatistics } from '../../../../TsLib/sq-common/backtestStatistics';
import { ChrtGenBacktestResult, UiChrtGenPrtfRunResult, CgTimeSeries, SqLog, ChartResolution, UiChartPoint, FolderJs, PortfolioJs, prtfsParseHelper, fldrsParseHelper, TreeViewState, TreeViewItem, createTreeViewData, PrtfItemType, LineStyle, ChartJs, SeasonalityData, getSeasonalityData, getDetailedStats, SqLogLevel } from '../../../../TsLib/sq-common/backtestCommon';
import { SqTreeViewComponent } from '../../../sq-ng-common/src/lib/sq-tree-view/sq-tree-view.component';
import { isValidDay, isValidMonth, isValidYear, parseNumberToDate, widthResizer, heightResizer, } from '../../../../TsLib/sq-common/utils-common';
import { SqChart } from '../../../../TsLib/sq-common/sqChart';

type Nullable<T> = T | null;

class HandshakeMessage {
  public email = '';
  public anyParam = -1;
  public prtfsToClient: Nullable<PortfolioJsEx[]> = null;
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

class PortfolioJsEx extends PortfolioJs {
  public leverage: number = 1.0; // default value
}

class BenchmarkEx {
  public ticker = '';
  public leverage: number = 1.0; // default value
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
  @ViewChild('startCalendarInput') startCalendarInput!: ElementRef<HTMLInputElement>;
  @ViewChild('startYearInput') startYearInput!: ElementRef<HTMLInputElement>;
  @ViewChild('startMonthInput') startMonthInput!: ElementRef<HTMLInputElement>;
  @ViewChild('startDayInput') startDayInput!: ElementRef<HTMLInputElement>;

  @ViewChild('endCalendarInput') endCalendarInput!: ElementRef<HTMLInputElement>;
  @ViewChild('endYearInput') endYearInput!: ElementRef<HTMLInputElement>;
  @ViewChild('endMonthInput') endMonthInput!: ElementRef<HTMLInputElement>;
  @ViewChild('endDayInput') endDayInput!: ElementRef<HTMLInputElement>;

  // Portfolios & BenchMark Sections
  m_treeViewState: TreeViewState = new TreeViewState();
  m_uiNestedPrtfTreeViewItems: TreeViewItem[] = [];
  m_allPortfolios: Nullable<PortfolioJsEx[]> = null;
  m_allFolders: Nullable<FolderJs[]> = null;
  m_sqStatisticsbuilder: SqStatisticsBuilder = new SqStatisticsBuilder();
  m_backtestStatsResults: StatisticsResults[] = [];
  m_detailedStatistics: DetailedStatistics = new DetailedStatistics();
  m_backtestedPortfolios: PortfolioJsEx[] = [];
  m_backtestedBenchmarks: BenchmarkEx[] = [];

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
  // Replacing m_startDateStr with m_startDateObj wrapper ojbect to be able to explicitly pass it as reference to onchange() callbacks.
  // JavaScript/TypeScript passes primitive values (like strings) by value, not by reference. If onchange() callback receives it as a string parameter (by value), we cannot change it inside the function.
  // Wrapping the dateStr into an object allows modifying the date within the object. see. https://grok.com/share/c2hhcmQtMg%3D%3D_d9efcb2e-2361-43c0-8d94-d1f477b7e390
  m_startDateObj: { dateStr: string } = {dateStr: ''};
  m_endDateObj: { dateStr: string } = {dateStr: ''};
  m_rangeSelection: string[] = ['YTD', '1M', '1Y', '3Y', '5Y', '10Y', 'ALL'];
  m_histRangeSelected: string = 'ALL';

  m_isSrvConnectionAlive: boolean = true;
  m_chrtGenDiagnosticsMsg: string = 'Benchmarking time, connection speed';
  m_isProgressBarVisble: boolean = false;
  m_isBacktestReturned: boolean = false;
  m_isPrtfSelectionDialogVisible: boolean = false;

  m_seasonalityData: SeasonalityData[] = []; // Seasonality
  m_userWarning: string | null = null;
  m_hasSqLogErrOrWarn: boolean = false;

  // Sample data for sqChart developing
  chartData: UiChartPoint[][] = [
    [
      { date: new Date('2021-01-01'), value: 100 },
      { date: new Date('2021-02-01'), value: 150 },
      { date: new Date('2021-03-01'), value: 120 },
      { date: new Date('2021-04-01'), value: 110 },
      { date: new Date('2022-03-01'), value: 200 },
      { date: new Date('2022-04-01'), value: 150 },
      { date: new Date('2023-05-01'), value: 200 },
      { date: new Date('2023-06-01'), value: 150 },
      { date: new Date('2024-07-01'), value: 2000 },
      { date: new Date('2024-08-01'), value: 100 },
      { date: new Date('2025-05-01'), value: 175 },
    ],
    [
      { date: new Date('2021-01-01'), value: 105 },
      { date: new Date('2021-02-01'), value: 155 },
      { date: new Date('2021-03-01'), value: 125 },
      { date: new Date('2021-04-01'), value: 115 },
      { date: new Date('2022-03-01'), value: 205 },
      { date: new Date('2022-04-01'), value: 155 },
      { date: new Date('2023-05-01'), value: 205 },
      { date: new Date('2023-06-01'), value: 155 },
      { date: new Date('2024-07-01'), value: 205 },
      { date: new Date('2024-08-01'), value: 105 },
      { date: new Date('2026-05-01'), value: 180 },
    ]];

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
      const bmrkItem = new BenchmarkEx();
      bmrkItem.ticker = item;
      this.m_backtestedBenchmarks.push(bmrkItem);
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
              if (id == parseInt(prtfStrIds[i])) {
                this.m_allPortfolios[j].leverage = 1.0; // default value
                this.m_backtestedPortfolios.push(this.m_allPortfolios[j]);
              }
            }
          }
          this.drawSqChart();
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
    this.m_seasonalityData.length = 0; // Resetting m_seasonalityData to an empty array
    this.m_detailedStatistics.backtestDetailedStatistics.length = 0; // Resetting backtestDetailedStatistics to an empty array
    const uiPrtfResItem = new UiChrtGenPrtfRunResult();
    gChrtGenDiag.serverBacktestTime = chrtGenBacktestRes.serverBacktestTimeMs;

    for (const item of chrtGenBacktestRes.pfRunResults) {
      const { firstVal: firstValDate, lastVal: lastValDate } = this.getDateRangeFromChrtData(item.chrtData);
      this.updateMinMaxDates(firstValDate, lastValDate);
      if (item.chrtData.chartResolution == ChartResolution.Minute || item.chrtData.chartResolution == ChartResolution.Minute5) // Check if the portfolio is of per minute resolution
        this.m_userWarning = 'PerMinute strategies not fully supported';
      const filteredPortfolios: PortfolioJsEx[] = this.m_backtestedPortfolios.filter((prtf) => prtf.name == item.name);// The m_backtestedPortfolios may contain the same portfolio name with different leverage values. So, we need to execute createCgTimeSeriesFromChrtData2() twice using the same chartData but with different leverage values.
      if (filteredPortfolios.length == 0) { // The backtestedPortfolios is populated only after "onConnected," meaning no portfolios will be added until the handshake provides the allPortfolios information (this.m_allPortfolios = handshakeMsg.prtfsToClient).
        const leverage = 1.0;
        const chartItem = this.createCgTimeSeriesFromChrtData(item.chrtData, item.name, true, leverage);
        uiPrtfResItem.prtfChrtValues.push(chartItem);
      } else {
        for (const prtf of filteredPortfolios) {
          const leverage = prtf.leverage;
          const chartItem = this.createCgTimeSeriesFromChrtData(item.chrtData, item.name, true, leverage);
          uiPrtfResItem.prtfChrtValues.push(chartItem);
        }
      }
      this.m_seasonalityData.push(getSeasonalityData(item.chrtData));
      this.m_detailedStatistics.backtestDetailedStatistics.push(getDetailedStats(item.chrtData, item.name));
    }

    for (const bmrkItem of chrtGenBacktestRes.bmrkHistories) {
      const { firstVal: firstValDate, lastVal: lastValDate } = this.getDateRangeFromChrtData(bmrkItem.chrtData);
      this.updateMinMaxDates(firstValDate, lastValDate);
      const filteredBenchmarks: BenchmarkEx[] = this.m_backtestedBenchmarks.filter((benchmark) => benchmark.ticker == bmrkItem.sqTicker);
      for (const benchmark of filteredBenchmarks) {
        const leverage = benchmark.leverage;
        const chartItem = this.createCgTimeSeriesFromChrtData(bmrkItem.chrtData, bmrkItem.sqTicker, false, leverage);
        uiPrtfResItem.bmrkChrtValues.push(chartItem);
      }
      this.m_seasonalityData.push(getSeasonalityData(bmrkItem.chrtData));
      this.m_detailedStatistics.backtestDetailedStatistics.push(getDetailedStats(bmrkItem.chrtData, bmrkItem.sqTicker));
    }

    this.getAnnualReturnYears(this.m_detailedStatistics.backtestDetailedStatistics, this.m_detailedStatistics.annualReturnYears); // Populate the annualReturnYears after the backtestDetailedStatistics for all portfolios and benchmarks have been received.
    for (const log of chrtGenBacktestRes.logs) {
      const logItem = new SqLog();
      logItem.sqLogLevel = log.sqLogLevel;
      logItem.message = log.message;
      uiPrtfResItem.sqLogs.push(logItem);
    }

    this.m_hasSqLogErrOrWarn = false; // reset the hasSqLoErrOrWarn
    for (const log of chrtGenBacktestRes.logs) {
      if (log.sqLogLevel == SqLogLevel.Error || log.sqLogLevel == SqLogLevel.Warn) { // check if there are any logLevels with error or warn state
        this.m_hasSqLogErrOrWarn = true;
        break;
      }
    }

    uiChrtGenPrtfRunResults.push(uiPrtfResItem);

    const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
    const prtfAndBmrkChrtData: CgTimeSeries[] = uiChrtGenPrtfRunResults[0].prtfChrtValues.concat(uiChrtGenPrtfRunResults[0].bmrkChrtValues);
    const lineChrtTooltip = document.getElementById('tooltipChart') as HTMLElement;

    this.m_startDate = this.m_minStartDate;
    this.m_endDate = this.m_maxEndDate;
    this.m_startDateObj.dateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_startDate);
    this.m_endDateObj.dateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_endDate);
    this.initializeSqIsoDateInputs();
    this.m_histRangeSelected = 'ALL';
    this.m_ultimateChrt.Init(lineChrtDiv, lineChrtTooltip, prtfAndBmrkChrtData);
    this.m_sqStatisticsbuilder.Init(prtfAndBmrkChrtData);
    this.onStartOrEndDateChanged(); // will recalculate CAGR and redraw chart
  }

  // Common function for both portfolios and bmrks to create UiChartPiont data from chartdata and index
  createUiChartPointFromChrtData(chrtData: ChartJs, index: number): UiChartPoint {
    const chrtpoint = new UiChartPoint();

    if (chrtData.dateTimeFormat == 'YYYYMMDD')
      chrtpoint.date = parseNumberToDate(chrtData.dates[index]);
    else if (chrtData.dateTimeFormat.includes('DaysFrom')) {
      const dateStartInd = chrtData.dateTimeFormat.indexOf('m');
      const dateStartsFrom = parseNumberToDate(parseInt(chrtData.dateTimeFormat.substring(dateStartInd + 1)));
      chrtpoint.date = new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + chrtData.dates[index]));
    } else
      chrtpoint.date = new Date(chrtData.dates[index] * AppComponent.cSecToMSec); // data comes as seconds. JS uses milliseconds since Epoch.
    chrtpoint.value = chrtData.values[index];
    return chrtpoint;
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
      const uniquePrtfIds: number[] = [];
      for (let i = 0; i < this.m_backtestedPortfolios.length; i++) {
        const id = this.m_backtestedPortfolios[i].id - this.gPortfolioIdOffset;
        if (!uniquePrtfIds.includes(id))
          uniquePrtfIds.push(id);
      }
      const uniqueBmrks: string[] = [];
      for (let i = 0; i < this.m_backtestedBenchmarks.length; i++) {
        if (!uniqueBmrks.includes(this.m_backtestedBenchmarks[i].ticker))
          uniqueBmrks.push(this.m_backtestedBenchmarks[i].ticker);
      }
      this.onStartBacktests();
      this._socket.send('RunBacktest:' + '?pids=' + uniquePrtfIds.toString() + '&bmrks=' + uniqueBmrks.toString()); // parameter example can be pids=1,13,6&bmrks=SPY,QQQ&start=20210101&end=20220305
    }
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
    this.m_startDateObj.dateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_startDate);
    this.m_endDateObj.dateStr = SqNgCommonUtilsTime.Date2PaddedIsoStr(this.m_maxEndDate); // Interestingly, when we change this which is bind to the date input html element, then the onChangeStartOrEndDate() is not called.
    this.m_endDate = this.m_maxEndDate;
    this.initializeSqIsoDateInputs();
    this.onStartOrEndDateChanged();
  }

  onUserChangedStartOrEndDateWidgets() { // User entry in the input field
    this.m_startDate = new Date(this.m_startDateObj.dateStr);
    this.m_endDate = new Date(this.m_endDateObj.dateStr);
    this.onStartOrEndDateChanged();
  }

  onClickClearBacktestedPortfolios() { // clear the user selected backtested portfolios
    this.m_backtestedPortfolios.length = 0;
  }

  onClickBmrkSelectedForBacktest(benchmarkStr: string) {
    if (benchmarkStr == '')
      return;
    const bmrkArray: string[] = benchmarkStr.trim().split(',');
    for (const item of bmrkArray) {
      const bmrkItem: BenchmarkEx = new BenchmarkEx();
      bmrkItem.ticker = item;
      this.m_backtestedBenchmarks.push(bmrkItem);
    }
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
      if (portfolioItem != null) {
      // Clone the portfolio item to avoid shared references
        const clonedPortfolio = { ...portfolioItem, leverage: 1.0 }; // Directly pushing reference objects from this.m_allPortfolios into this.m_backtestedPortfolios results in shared references between the two arrays, causing updates in one array to reflect in the other. To prevent this, we need to create a deep clone of the portfolioItem before adding it to this.m_backtestedPortfolios. Instead of directly adding the portfolioItem, a shallow copy ({ ...portfolioItem }) is created.
        this.m_backtestedPortfolios.push(clonedPortfolio);
      }
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

  getAnnualReturnYears(backtestDetailedStatistics: BacktestDetailedStatistics[], annualReturnYears: number[]) {
    annualReturnYears.length = 0; // Reset the annualReturnYears array by clearing its contents
    for (const detailedStat of backtestDetailedStatistics) { // Loop through each backtestDetailedStatistics item
      for (const annualReturn of detailedStat.annualReturns) { // Loop through each item's annualReturns array
        const year: number = annualReturn.year;
        if (!annualReturnYears.includes(year)) // Check if the year is not already in the annualReturnYears array
          annualReturnYears.push(year); // If the year is unique, add it to the array
      }
    }
    annualReturnYears.sort((year1: number, year2: number) => year2 - year1); // Sort the annualReturnYears array in descending order. This ensures the latest year data appears first in the list
  }

  onChangePrtfLeverage(event: Event, prtfItem: PortfolioJsEx) {
    const leverage = parseFloat((event.target as HTMLInputElement).value.trim());
    prtfItem.leverage = leverage;
  }

  onChangeBmrkLeverage(event: Event, bmrkItem: BenchmarkEx) {
    const leverage = parseFloat((event.target as HTMLInputElement).value.trim());
    bmrkItem.leverage = leverage;
  }

  // Common function for both portfolios and bmrks to create chartGenerator TimeSeries data
  createCgTimeSeriesFromChrtData(chrtData: ChartJs, name: string, isPrimary: boolean, leverage: number): CgTimeSeries {
    const cgTimeSeries = new CgTimeSeries();
    cgTimeSeries.name = name;
    cgTimeSeries.chartResolution = ChartResolution[chrtData.chartResolution];
    cgTimeSeries.linestyle = isPrimary ? LineStyle.Solid : LineStyle.Dashed;
    cgTimeSeries.isPrimary = isPrimary;
    cgTimeSeries.priceData = [];

    for (let i = 0; i < chrtData.dates.length; i++) {
      const chrtPoint: UiChartPoint = this.createUiChartPointFromChrtData(chrtData, i);
      cgTimeSeries.priceData.push(chrtPoint);
    }

    const isLeveraged: boolean = leverage != 1;
    if (isLeveraged) {
      cgTimeSeries.name = `${name} ${leverage}x`; // If leveraged, append the leverage value to the portfolio/benchmark name. e.g., if the benchmark is SPY and leverage is 3, then name = SPY 3x.
      let preVal: number = cgTimeSeries.priceData[0].value; // Initialize preVal with the first value in the array
      for (let i = 1; i < cgTimeSeries.priceData.length; i++) {
        const curVal: number = cgTimeSeries.priceData[i].value;
        const pctChg: number = curVal / preVal - 1;
        const pctChgLev: number = pctChg * leverage;
        cgTimeSeries.priceData[i].value = cgTimeSeries.priceData[i - 1].value * (1 + pctChgLev);
        preVal = curVal; // Update preVal for the next iteration
      }
    }
    return cgTimeSeries;
  }

  initializeSqIsoDateInputs(): void {
    // Initialize start date
    const [startYear, startMonth, startDay] = this.m_startDateObj.dateStr.split('-');
    this.startYearInput.nativeElement.value = startYear;
    this.startMonthInput.nativeElement.value = startMonth;
    this.startDayInput.nativeElement.value = startDay;
    this.startCalendarInput.nativeElement.value = this.m_startDateObj.dateStr;
    // Initialize end date
    const [endYear, endMonth, endDay] = this.m_endDateObj.dateStr.split('-');
    this.endYearInput.nativeElement.value = endYear;
    this.endMonthInput.nativeElement.value = endMonth;
    this.endDayInput.nativeElement.value = endDay;
    this.endCalendarInput.nativeElement.value = this.m_endDateObj.dateStr;
  }

  onChangeDateFromCalendarPicker(calendarInput: HTMLInputElement, yearInput: HTMLInputElement, monthInput: HTMLInputElement, dayInput: HTMLInputElement, dateObj: { dateStr: string }): void {
    const [year, month, day] = calendarInput.value.split('-');
    // Update the year, month, and day inputs based on the date selected by the user from the calendar
    yearInput.value = year;
    monthInput.value = month;
    dayInput.value = day;
    dateObj.dateStr = calendarInput.value;
    this.onUserChangedStartOrEndDateWidgets();
  }

  onChangeDatePart(type: 'year' | 'month' | 'day', yearInput: HTMLInputElement, monthInput: HTMLInputElement, dayInput: HTMLInputElement, calendarInput: HTMLInputElement, dateObj: { dateStr: string }) {
    const usedDate: Date = new Date(calendarInput.value);

    switch (type) {
      case 'year':
        const yearInputVal = yearInput.value.trim();
        if (isValidYear(yearInputVal))
          usedDate.setFullYear(parseInt(yearInputVal, 10));
        else // If the year is invalid (e.g., the user typed a non-numeric character like 'z'). We force back the old date part from the calendarInput into the inputElement)
          yearInput.value = usedDate.getFullYear().toString();
        break;
      case 'month':
        const monthInputVal = parseInt(monthInput.value.trim(), 10).toString().padStart(2, '0'); // Pad single-digit month to 2 digits before validation
        if (isValidMonth(monthInputVal)) {
          // Issue: When a user changes the month of an existing date without modifying the day (e.g., 2010-06-30 → 2010-02-30),
          // the code currently shifts the month to March (2010-03-02) instead of correcting the invalid day.
          // To fix this, determine the maximum valid day for the new month before updating the month, and adjust `usedDate` accordingly.
          const newMonth = parseInt(monthInputVal, 10) - 1; // Subtracting 1 from the month value since JavaScript months are 0-indexed
          const usedYear = usedDate.getFullYear();
          const usedDay = usedDate.getDate();
          const lastDayOfNewMonth = new Date(usedYear, newMonth + 1, 0).getDate(); // Get the last day of the new month
          if (usedDay > lastDayOfNewMonth) { // Adjust the day if the usedDay is greater than the lastDay of the newMonth
            usedDate.setDate(lastDayOfNewMonth);
            dayInput.value = lastDayOfNewMonth.toString().padStart(2, '0');
          }
          usedDate.setMonth(newMonth);
          monthInput.value = monthInputVal;
        } else // If the month is invalid (e.g., the user typed a non-numeric character like 'z'). We force back the old date part from the calendarInput into the inputElement)
          monthInput.value = (usedDate.getMonth() + 1).toString().padStart(2, '0');
        break;
      case 'day':
        const dayInputVal = parseInt(dayInput.value.trim(), 10).toString().padStart(2, '0'); // Pad single-digit day to 2 digits before validation
        if (isValidDay(dayInputVal, usedDate)) {
          usedDate.setDate(parseInt(dayInputVal, 10));
          dayInput.value = dayInputVal;
        } else // If the day is invalid (e.g., the user typed a non-numeric character like 'z'). We force back the old date part from the calendarInput into the inputElement)
          dayInput.value = usedDate.getDate().toString().padStart(2, '0');
        break;
    }
    calendarInput.value = usedDate.toISOString().substring(0, 10);
    dateObj.dateStr = calendarInput.value;
    // onUserChangedStartOrEndDateWidgets() call requires that m_startDateStr, m_endDateStr are already updated.
    // So, 2-way data binding [(ngModel)]="m_startDateStr" wouldn't help, because that would change m_startDateStr too late. We have to change them right now, at this point of execution.
    // Also, assigning it in HTML ((change)="m_startDateStr = onChangeDateFromCalendarPicker(...)" wouldn't help, because that is too late.
    // The only thing that would help is to Wrap m_startDateStr in an Object:
    // m_startDateObj = { dateStr: '' }; // Wrap the string in an object, then you can pass that object in HTML template function as a reference (not value)
    this.onUserChangedStartOrEndDateWidgets();
  }

  drawSqChart() {
    // Get the chart container
    const chartDiv: HTMLElement = document.getElementById('chartContainer') as HTMLElement;
    const widthResizerDiv: HTMLElement = document.getElementById('widthResizer') as HTMLElement;
    const heightResizerDiv: HTMLElement = document.getElementById('heightResizer') as HTMLElement;
    // Create and initialize the chart
    const chart = new SqChart();
    chart.init(chartDiv);
    // Add a data series
    chart.addLine(this.chartData);
    // chart.addLine(this.chartData.dataset2);
    // Set viewport to show data between two dates
    const startDate: Date = new Date('2021-01-01');
    const endDate: Date = new Date('2023-08-01');
    chart.setViewport(startDate, endDate);
    // resizing
    widthResizer(chartDiv, widthResizerDiv);
    heightResizer(chartDiv, heightResizerDiv);
  }
}