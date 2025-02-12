import * as d3 from 'd3';
import { prtfRunResultChrt } from '../sq-common/chartAdvanced';
import { parseNumberToDate } from './utils-common';
import { SqNgCommonUtilsTime } from '../../projects/sq-ng-common/src/lib/sq-ng-common.utils_time';
import { sqAverageOfAnnualReturns, sqAverageOfSeasonalityData, sqMedian } from './utils_math';
import { AnnualReturn, BacktestDetailedStatistics } from './backtestStatistics';

// ************************************************ //
// Classes used for developing charts, stats and positions of PortfolioRunResults
// The below classes are used in PortfolioManager and ChartGenerator Apps

// Input data classes
type Nullable<T> = T | null;
const cSecToMSec = 1000;

export enum SqLogLevel {
  Off = 'Off',
  Trace = 'Trace',
  Debug = 'Debug',
  Info = 'Info',
  Warn = 'Warn',
  Error = 'Error',
  Fatal = 'Fatal'
}

export enum LineStyle {
  Solid = 'Solid',
  Dotted = 'Dotted',
  Dashed = 'Dashed',
  DashDot = 'DashDot'
}

export class SqLog {
  public sqLogLevel: SqLogLevel = SqLogLevel.Info;
  public message = '';
}

export enum ChartResolution {
    Second, Minute, Minute5, Hour, Daily, Weekly, Monthly
}

export interface ChartJs { // PfRunResults Chart Data
  dates: number[];
  values: number[];
  chartResolution: ChartResolution;
  dateTimeFormat: string;
}

export class ChrtGenBacktestResult {
  public pfRunResults!: ChrtGenPfRunResult[];
  public bmrkHistories!: any[];
  public logs!: any[];
  public serverBacktestTimeMs!: number;
}

export class ChrtGenPfRunResult {
  public pstat: any; // all the Stat members from UiPrtfRunResult, we skip creating detailed sub classes
  public chrtData!: ChartJs;
  public name!: string;
}

export class PrtfRunResultJs extends ChrtGenPfRunResult { // we can specify the input types more, but whatever.
  public prtfPoss: any; // all the position members from UiPrtfPositions, we skip creating detailed sub classes
  public logs: SqLog[] = [];
}

// Ui classes
export class UiPrtfRunResultCommon { // this class is common for portfolioManager and chartGenerator apps
  public startPortfolioValue: number = 0;
  public endPortfolioValue: number = 0;
  public totalReturn: number = 0;
  public cAGR: number = 0;
  public maxDD: number = 0;
  public sharpe: number = 0;
  public cagrSharpe: number = 0;
  public stDev: number = 0;
  public ulcer: number = 0;
  public tradingDays: number = 0;
  public nTrades: number = 0;
  public winRate: number = 0;
  public lossRate: number = 0;
  public sortino: number = 0;
  public turnover: number = 0;
  public longShortRatio: number = 0;
  public fees: number = 0;
  public benchmarkCAGR: number = 0;
  public benchmarkMaxDD: number = 0;
  public correlationWithBenchmark: number = 0;
  public sqLogs: SqLog[] = [];
}

export class UiChrtGenPrtfRunResult extends UiPrtfRunResultCommon {
  public prtfChrtValues: CgTimeSeries[] = []; // used in backtestResults in chrtGen app
  public bmrkChrtValues: CgTimeSeries[] = []; // used in backtestResults in chrtGen app
}

export class UiPrtfRunResult extends UiPrtfRunResultCommon { // PrtfRun Results requires position values to display
  public chrtValues: UiChartPoint[] = []; // used in PrtfRunResults in portfolioManager app
  public prtfPosValues: UiPrtfPositions[] = [];

  public onDateTwrPv: number = 0;
  public prevDateTwrPv: number = 0;
  public onDatePosPv: number = 0; // Positions-$PV = $PV on Date (ClosePr or RT)
  public prevDatePosPv: number = 0; // Positions-$PV on PrevDate (ClosePr)
}

// chart values
export class UiChartPoint {
  public date = new Date();
  public value = NaN;
}

export class CgTimeSeries {
  public name: string = '';
  public chartResolution: string = '';
  public priceData: UiChartPoint[] = [];
  public linestyle: LineStyle = LineStyle.Solid;
  public isPrimary: boolean = false; // to set the brighter colors for primary items(portfolios) and lighter colors for secondary items(benchmarks).
}

export class UiPrtfPositions {
  public sqTicker: string = '';
  public name: string = ''; // shortName
  public quantity: number = 0;
  public avgPrice: number = 0;
  public priorClose: number = NaN;
  public estPrice: number = NaN;
  public pctChgTod: number = NaN;
  public plTod: number = NaN;
  public costBasis: number = 0; // the holding cost
  public mktVal: number = 0;
  public plPctTotal: number = NaN;
  public plTotal: number = NaN;
  public sharesOutstanding: number = 0;
  public marketCap: number = 0;
}

export enum PrtfItemType { // for differenting the folder and portfolio
  Folder = 'Folder',
  Portfolio = 'Portfolio'
 }

export class PortfolioItemJs {
  public id = -1;
  public name = '';
  public ownerUserId = -1;
  public parentFolderId = -1;
  public creationTime = '';
  public note = '';
  public prtfItemType: PrtfItemType = PrtfItemType.Folder; // need a default for compilation
}

export class FolderJs extends PortfolioItemJs {
}

export class PortfolioJs extends PortfolioItemJs {
  public sharedAccess = 'Restricted'; // default access type
  public sharedUserWithMe = '';
  public baseCurrency = 'USD'; // default currrency
  public type = 'Trades'; // default portfolioType
  public algorithm = '';
  public algorithmParam = '';
  public tradeHistoryId: number = -1; // default value
  public legacyDbPortfName = '';
}

export class TreeViewItem { // future work. At the moment, it copies PortfolioFldrJs[] and add the children field. With unnecessary field values. When Portfolios are introduced, this should be rethought.
  // PortfolioItemJs specific fields
  public id = -1;
  public name = '';
  public ownerUserId = -1;
  public parentFolderId = -1;

  public prtfItemType: PrtfItemType = PrtfItemType.Folder;

  // TreeViewItem specific fields
  public children: TreeViewItem[] = []; // children are other TreeViewItems
  public isSelected: boolean = false;
  public isExpanded: boolean = false;
  public isCheckboxChecked: boolean = false;
}

export class TreeViewState {
  public lastSelectedItem : Nullable<TreeViewItem> = null;
  public lastSelectedItemId: number = -1; // need to remember the lastselectedItemId to highlight the user Selected item even after refresh or after creating/editing an item
  public expandedPrtfFolderIds: number[] = [];
}

// used in PrtfVwr to process the update the HistPrice from YF
export class TickerClosePrice {
  public date: Date = new Date();
  public closePrice: number = 0;
}

export enum TradeAction {
  Unknown = 0,
  Deposit = 1,
  Withdrawal = 2,
  Buy = 3,
  Sell = 4,
  Exercise = 5,
  Expired = 6
}

export enum AssetType {
  Unknown = 0,
  CurrencyCash = 1,
  CurrencyPair = 2,
  Stock = 3,
  Bond = 4,
  Fund = 5,
  Futures = 6,
  Option = 7,
  Commodity = 8,
  RealEstate = 9,
  FinIndex = 10,
  BrokerNAV = 11,
  Portfolio = 12,
  GeneralTimeSeries = 13,
  Company = 14
}

export enum CurrencyId {
  Unknown = 0,
  USD = 1,
  EUR = 2,
  GBP = 3,
  GBX = 4,
  HUF = 5,
  JPY = 6,
  CNY = 7,
  CAD = 8,
  CHF = 9
}

export enum ExchangeId {
  Unknown = -1,
  NASDAQ = 1,
  NYSE = 2,
  AMEX = 3,
  PINK = 4,
  CDNX = 5,
  LSE = 6,
  XTRA = 7,
  CBOE = 8,
  ARCA = 9,
  BATS = 10,
  OTCBB = 11,
}

// used in PrtfVwr and ChrtGen to process seasonality matrix
export class MonthlySeasonality {
  year: number = 0;
  returns: number[] = []; // 12 elements for January..December returns in the given year.
}

export class WeeklySeasonality {
  year: number = 0;
  returns: number[] = [];
}

export class SeasonalityData { // Container Class of every data about seasonality.
  monthlySeasonality: MonthlySeasonality[] = []; // E.g. if data is from 2023 to 2024, then this array has 2 MonthlySeasonality items for those years. Inside them, there are 12 items for the 12 months.
  // Initialize the arrays with pre-allocated memory for 12 elements, one for each month, filled with NaN.This avoids dynamic resizing compared to an empty array.
  monthlySeasonalityWinrate: number[] = new Array(12).fill(NaN);
  monthlySeasonality3yAvg: number[] = new Array(12).fill(NaN);
  monthlySeasonality5yAvg: number[] = new Array(12).fill(NaN);
  monthlySeasonality10yAvg: number[] = new Array(12).fill(NaN);
  monthlySeasonalityAvg: number[] = new Array(12).fill(NaN);
  monthlySeasonalityMedian: number[] = new Array(12).fill(NaN);

  weeklySeasonality: WeeklySeasonality[] = [];
}

export class UiSeasonalityChartPoint {
  month: string = '';
  mean: number = 0;
  median: number = 0;
}

// ************************************************ //
export function prtfsParseHelper(_this: any, key: string, value: any): boolean { // return value is isRemoveOriginal
  // eslint-disable-next-line no-invalid-this
  // const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used
  if (key === 'n') {
    _this.name = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'ouId') {
    _this.ownerUserId = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'p') {
    _this.parentFolderId = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'cTime') {
    _this.creationTime = value;
    return true; // if return undefined, original property will be removed
  }

  if (key === 'sAcs') {
    _this.sharedAccess = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'sUsr') {
    _this.sharedUserWithMe = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'bCur') {
    _this.baseCurrency = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'algo') {
    _this.algorithm = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'algoP') {
    _this.algorithmParam = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'trdHis') {
    _this.tradeHistoryId = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'lgcyDbPf') {
    _this.legacyDbPortfName = value;
    return true; // if return undefined, original property will be removed
  }
  return false; // return value is isRemoveOriginal. In general, we don't remove values
}

export function fldrsParseHelper(_this: any, key: string, value: any): boolean {
  if (key === 'n') {
    _this.name = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'ouId') {
    _this.ownerUserId = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'p') {
    _this.parentFolderId = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'cTime') {
    _this.creationTime = value;
    return true; // if return undefined, original property will be removed
  }
  return false;
}

export function fundamentalDataParseHelper(_this: any, key: string, value: any): boolean {
  if (key === 'sn') {
    _this.name = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'sOut') {
    _this.sharesOutstanding = value;
    return true; // if return undefined, original property will be removed
  }
  return false;
}

export function createTreeViewData(pFolders: Nullable<FolderJs[]>, pPortfolios: Nullable<PortfolioJs[]>, pTreeViewState: TreeViewState) : TreeViewItem[] {
  if (!(Array.isArray(pFolders) && pFolders.length > 0 ) || !(Array.isArray(pPortfolios) && pPortfolios.length > 0 ))
    return [];

  console.log('pTreeViewState: isSelected', pTreeViewState.lastSelectedItem?.isSelected);
  const treeviewItemsHierarchyResult: TreeViewItem[] = [];
  const tempPrtfItemsDict = {}; // stores the portfolio items temporarly

  for (let i = 0; i < pFolders.length; i++) { // adding folders data to tempPrtfItemsDict
    const fldrItem : FolderJs = pFolders[i];
    tempPrtfItemsDict[fldrItem.id] = fldrItem;
  }

  for (let j = 0; j < pPortfolios.length; j++) { // adding portfolios data to tempPrtfItemsDict
    const prtfItem : PortfolioJs = pPortfolios[j];
    tempPrtfItemsDict[prtfItem.id] = prtfItem;
  }

  for (const id of Object.keys(tempPrtfItemsDict)) // empty the childen array of each item
    tempPrtfItemsDict[id]['children'] = []; // we cannot put this into the main loop, because we should not delete the Children array of an item that comes later.

  for (const id of Object.keys(tempPrtfItemsDict)) {
    const item : TreeViewItem = tempPrtfItemsDict[id];
    item.isSelected = false;
    if (pTreeViewState.lastSelectedItemId == item.id) // it should not break out, because we need to assign item.isSelected = false for all the other items which are not selected. Otherwise the tree is not assinged with the isSelected state and looks weird on the UI
      item.isSelected = true;

    item.isExpanded = false;
    for (let i = 0; i < pTreeViewState.expandedPrtfFolderIds.length; i++) { // expanded folder Id's check
      if (pTreeViewState.expandedPrtfFolderIds[i] == item.id) {
        item.isExpanded = true;
        break;
      }
    }

    const parentItem: TreeViewItem = tempPrtfItemsDict[item.parentFolderId]; // No Folder has id of -1. If a ParentFolderID == -1, then that item is at the root level, and we say it has no parent, and parentItem is undefined
    if (parentItem != undefined) // if item has a proper parent (so its parentFolderId is not -1)
      parentItem.children.push(item); // add ourselves as a child to the parent object
    else
      treeviewItemsHierarchyResult.push(item); // item is at root level. Add to the result list.
  }

  return treeviewItemsHierarchyResult;
};

export function statsParseHelper(_this: any, key: string, value: any): boolean {
  if (key === 'startPv') {
    _this.startPortfolioValue = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'endPv') {
    _this.endPortfolioValue = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'shrp') {
    _this.sharpeRatio = value == 'NaN' ? NaN : parseFloat(value);
    return true; // if return undefined, original property will be removed
  }
  if (key === 'cagrShrp') {
    _this.cagrSharpe = parseFloat(value);
    return true; // if return undefined, original property will be removed
  }
  if (key === 'tr') {
    _this.totalReturn = parseFloat(value);
    return true; // if return undefined, original property will be removed
  }
  if (key === 'wr') {
    _this.winRate = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'lr') {
    _this.lossingRate = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'srtn') {
    _this.sortino = value == 'NaN' ? NaN : parseFloat(value);
    return true; // if return undefined, original property will be removed
  }
  if (key === 'to') {
    _this.turnover = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'ls') {
    _this.longShortRatio = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'bCAGR') {
    _this.benchmarkCAGR = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'bMax') {
    _this.benchmarkMaxDD = value;
    return true; // if return undefined, original property will be removed
  }
  if (key === 'cwb') {
    _this.correlationWithBenchmark = value;
    return true; // if return undefined, original property will be removed
  }
  return false;
}

export function updateUiWithPrtfRunResultUntilDate(prtfRunResult: Nullable<PrtfRunResultJs>, uiPosPrtfRunResult: UiPrtfRunResult, histDateStr: string) {
  if (prtfRunResult == null)
    return;

  uiPosPrtfRunResult.prtfPosValues.length = 0;
  for (let i = 0; i < prtfRunResult.prtfPoss.length; i++) {
    const posItem = new UiPrtfPositions();
    posItem.sqTicker = prtfRunResult.prtfPoss[i].sqTicker;
    posItem.quantity = prtfRunResult.prtfPoss[i].quantity;
    posItem.avgPrice = prtfRunResult.prtfPoss[i].avgPrice;
    posItem.priorClose = prtfRunResult.prtfPoss[i].backtestLastPrice;
    posItem.costBasis = Math.round(posItem.avgPrice * posItem.quantity);
    posItem.mktVal = Math.round(prtfRunResult.prtfPoss[i].estPrice * posItem.quantity); // mktVal - uses the RT price
    if (!posItem.sqTicker.startsWith('C')) { // excluding the Cash Tickers
      posItem.estPrice = prtfRunResult.prtfPoss[i].estPrice;
      posItem.pctChgTod = (posItem.estPrice - posItem.priorClose) / posItem.priorClose;
      posItem.plTod = Math.round(posItem.quantity * (posItem.estPrice - posItem.priorClose));
      posItem.plTotal = Math.round(posItem.quantity * (posItem.estPrice - posItem.avgPrice));
      posItem.plPctTotal = posItem.plTotal / Math.abs(posItem.costBasis);
    }
    uiPosPrtfRunResult.prtfPosValues.push(posItem);
  }

  uiPosPrtfRunResult.chrtValues.length = 0;
  for (let i = 0; i < prtfRunResult.chrtData.dates.length; i++) {
    const chartItem = new UiChartPoint();
    chartItem.date = convertToDateBasedOnDateFormat(prtfRunResult.chrtData.dates[i], prtfRunResult.chrtData.dateTimeFormat);
    chartItem.value = prtfRunResult.chrtData.values[i];
    uiPosPrtfRunResult.chrtValues.push(chartItem);
  }

  uiPosPrtfRunResult.onDatePosPv = 0;
  uiPosPrtfRunResult.prevDatePosPv = 0;
  uiPosPrtfRunResult.onDateTwrPv = 0;
  uiPosPrtfRunResult.prevDateTwrPv = 0;
  for (let i = 0; i < prtfRunResult.prtfPoss.length; i++) {
    const prtfPos = prtfRunResult.prtfPoss[i];
    uiPosPrtfRunResult.onDatePosPv += prtfPos.sqTicker.startsWith('C') ? prtfPos.backtestLastPrice * prtfPos.quantity : prtfPos.estPrice * prtfPos.quantity; // Calculate posPvOnDate
    uiPosPrtfRunResult.prevDatePosPv += prtfPos.backtestLastPrice * prtfPos.quantity; // Calculate posPvPrevDate
  }

  const chrtDataCount: number = prtfRunResult.chrtData.dates.length - 1;
  // Converting the date to a string format (yyyy-mm-dd) for comparison since we're only interested in the date part and not the time.
  const todayDateStr: string = new Date().toISOString().substring(0, 10);

  // Find the prevDate TWR-PV
  // The chartValues contain multiple entries for the same date towards the end.
  // To retrieve the previous date's value, we compare the dates and stop at the first match.
  for (let i = chrtDataCount; i >= 0; i--) {
    const chrtDateStr: string = SqNgCommonUtilsTime.Date2PaddedIsoStr(convertToDateBasedOnDateFormat(prtfRunResult.chrtData.dates[i], prtfRunResult.chrtData.dateTimeFormat));
    if (chrtDateStr < histDateStr) { // intentional string representation comparison. More lightweight than converting both to a Date, and it works, because we keep both dateStr in ISO format. It compares the character unicodes one by one.
      uiPosPrtfRunResult.prevDateTwrPv = prtfRunResult.chrtData.values[i];
      break;
    }
  }

  // Calculate onDateTwrPv
  if (histDateStr == todayDateStr) {
    const pvPctChgMultToday: number = uiPosPrtfRunResult.onDatePosPv / uiPosPrtfRunResult.prevDatePosPv;
    uiPosPrtfRunResult.onDateTwrPv = pvPctChgMultToday * uiPosPrtfRunResult.prevDateTwrPv;
  } else {
    for (let i = chrtDataCount; i >= 0; i--) { // Retrieve the onDateTwrPV where histDateStr matches chartDateStr
      const chrtDateStr: string = SqNgCommonUtilsTime.Date2PaddedIsoStr(convertToDateBasedOnDateFormat(prtfRunResult.chrtData.dates[i], prtfRunResult.chrtData.dateTimeFormat));
      if (histDateStr == chrtDateStr) {
        uiPosPrtfRunResult.onDateTwrPv = prtfRunResult.chrtData.values[i];
        break;
      }
    }
  }
}

export function updateUiWithPrtfRunResult(prtfRunResult: Nullable<PrtfRunResultJs>, uiPrtfRunResult: UiPrtfRunResult, uiChrtWidth: number, uiChrtHeight: number) {
  if (prtfRunResult == null)
    return;

  uiPrtfRunResult.startPortfolioValue = prtfRunResult.pstat.startPortfolioValue;
  uiPrtfRunResult.endPortfolioValue = prtfRunResult.pstat.endPortfolioValue;
  uiPrtfRunResult.totalReturn = prtfRunResult.pstat.totalReturn;
  uiPrtfRunResult.cAGR = parseFloat(prtfRunResult.pstat.cagr);
  uiPrtfRunResult.maxDD = parseFloat(prtfRunResult.pstat.maxDD);
  uiPrtfRunResult.sharpe = prtfRunResult.pstat.sharpeRatio;
  uiPrtfRunResult.cagrSharpe = prtfRunResult.pstat.cagrSharpe;
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

  uiPrtfRunResult.prtfPosValues.length = 0;
  for (let i = 0; i < prtfRunResult.prtfPoss.length; i++) {
    const posItem = new UiPrtfPositions();
    posItem.sqTicker = prtfRunResult.prtfPoss[i].sqTicker;
    posItem.quantity = prtfRunResult.prtfPoss[i].quantity;
    posItem.avgPrice = prtfRunResult.prtfPoss[i].avgPrice;
    posItem.priorClose = prtfRunResult.prtfPoss[i].backtestLastPrice;
    posItem.costBasis = Math.round(posItem.avgPrice * posItem.quantity);
    posItem.mktVal = Math.round(prtfRunResult.prtfPoss[i].estPrice * posItem.quantity); // mktVal - uses the RT price
    if (!posItem.sqTicker.startsWith('C')) { // excluding the Cash Tickers
      posItem.estPrice = prtfRunResult.prtfPoss[i].estPrice;
      posItem.pctChgTod = (posItem.estPrice - posItem.priorClose) / posItem.priorClose;
      posItem.plTod = Math.round(posItem.quantity * (posItem.estPrice - posItem.priorClose));
      posItem.plTotal = Math.round(posItem.quantity * (posItem.estPrice - posItem.avgPrice));
      posItem.plPctTotal = posItem.plTotal / Math.abs(posItem.costBasis);
    }
    uiPrtfRunResult.prtfPosValues.push(posItem);
  }

  uiPrtfRunResult.chrtValues.length = 0;
  for (let i = 0; i < prtfRunResult.chrtData.dates.length; i++) {
    const chartItem = new UiChartPoint();
    chartItem.date = convertToDateBasedOnDateFormat(prtfRunResult.chrtData.dates[i], prtfRunResult.chrtData.dateTimeFormat);
    chartItem.value = prtfRunResult.chrtData.values[i];
    uiPrtfRunResult.chrtValues.push(chartItem);
  }

  uiPrtfRunResult.sqLogs = prtfRunResult.logs;

  d3.selectAll('#pfRunResultChrt > *').remove();
  const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
  const margin = {top: 30, right: 30, bottom: 30, left: 50 };
  const chartWidth = uiChrtWidth * 0.98 - margin.left - margin.right; // 98% of the PanelChart Width
  const chartHeight = uiChrtHeight * 0.95 - margin.top - margin.bottom; // 95% of the PanelChart Height
  const chrtData = uiPrtfRunResult.chrtValues.map((r:{ date: Date; value: number; }) => ({date: new Date(r.date), value: r.value}));
  const xMin = d3.min(chrtData, (r:{ date: Date; }) => r.date);
  const xMax = d3.max(chrtData, (r:{ date: Date; }) => r.date);
  const yMinAxis = d3.min(chrtData, (r:{ value: number; }) => r.value);
  const yMaxAxis = d3.max(chrtData, (r:{ value: number; }) => r.value);

  prtfRunResultChrt(chrtData, lineChrtDiv, chartWidth, chartHeight, margin, xMin, xMax, yMinAxis, yMaxAxis);
}

// ************SeasonalityMatrix********************
// step1: group the data by year and month and get the last value of each month
// step2: calculate the monthly percentage return
// step2.1: example: yearMonth: 201012, value: 8.58, yearMonth: 201101, value: 13.04
// step2.2: return: 201101: 13.04/8.58 = 0.519
// step3: now store the year and return value in MonthlySeasonality: Object
// ex: seasonality: monthlySeasonality = [ { year:2011, returns:[0.519, 0.62, 0.75, ......] },]
// Step4: Winrate Calculation
// Step5: Average Calculation
// Step6: Median Calculation
// *****************************************************
export function getSeasonalityData(chartData: ChartJs): SeasonalityData {
  const histData: ChartJs = chartData;
  const seasonalityData = new SeasonalityData();
  if (histData == null)
    return seasonalityData;

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
      date = SqNgCommonUtilsTime.Date2PaddedIsoStr(new Date(histData.dates[i] * cSecToMSec)); // data comes as seconds. JS uses milliseconds since Epoch.
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
        if (monthlySeasonality[i].year == parseInt(year)) {
          existingSeasonality = monthlySeasonality[i];
          break;
        }
      }

      // If no existing seasonality data is found for the current year, create a new one
      if (existingSeasonality == undefined) {
        existingSeasonality = new MonthlySeasonality();
        existingSeasonality.year = parseInt(year);
        existingSeasonality.returns = new Array(12); // Initialize an empty array with 12 elements for each month of the year
        monthlySeasonality.push(existingSeasonality);
      }

      existingSeasonality.returns[monthIndex] = value;
    }

    seasonalityData.monthlySeasonality = monthlySeasonality;

    // Step4: Winrate Calculation
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

    // Step5: Average Calculation
    const numOfYears: number = monthlySeasonality.length; // Represents the number of years of data available, used to ensure sufficient data for calculating 3, 5, and 10-year averages, preventing potential crashes.
    seasonalityData.monthlySeasonality3yAvg = numOfYears >= 3 ? sqAverageOfSeasonalityData(monthlySeasonality, 3) : seasonalityData.monthlySeasonality3yAvg; // Calculate 3-year average
    seasonalityData.monthlySeasonality5yAvg = numOfYears >= 5 ? sqAverageOfSeasonalityData(monthlySeasonality, 5) : seasonalityData.monthlySeasonality5yAvg; // Calculate 5-year average
    seasonalityData.monthlySeasonality10yAvg = numOfYears >= 10 ? sqAverageOfSeasonalityData(monthlySeasonality, 10) : seasonalityData.monthlySeasonality10yAvg; // Calculate 10-year average
    seasonalityData.monthlySeasonalityAvg = sqAverageOfSeasonalityData(monthlySeasonality, numOfYears); // Calculate overall average (mean) for all available data

    // Step6: Median calculation
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

export function getDetailedStats(chartData: ChartJs, name: string): BacktestDetailedStatistics {
  const histData: ChartJs = chartData;
  const detailedStats: BacktestDetailedStatistics = new BacktestDetailedStatistics();
  if (histData == null)
    return detailedStats;

  let iStart: number = 0;
  const annualReturns: AnnualReturn[] = [];
  while (iStart < histData.dates.length - 1) { // if iStart == n - 1 (the last item), we can stop the outer loop, as there will be no new years.
    // Usually (except the first cycle), dates[iStart] points to 31st December. In that usual case the yearToSeek is obtained by the next date (2nd January).
    const yearToSeek: number = iStart == 0 ? convertToDateBasedOnDateFormat(histData.dates[0], histData.dateTimeFormat).getFullYear() : convertToDateBasedOnDateFormat(histData.dates[iStart + 1], histData.dateTimeFormat).getFullYear();
    let dateNext: Date;
    let iLast: number = iStart;
    do {
      iLast++;
      dateNext = convertToDateBasedOnDateFormat(histData.dates[iLast], histData.dateTimeFormat);
    } while (dateNext.getFullYear() == yearToSeek && iLast < histData.dates.length); // This loops until the Year matches, and exits at the first place when the Years differ. Or it exits at the end.

    iLast = iLast - 1; // we want the Last PV of the current year, so we move 1 step back. If we overwent the length (iLast == n), then we also come back 1 step to n - 1.

    // Usually (except the first and the last 'partial year' cycles), both iStart and iLast points to 31st December.
    const annualReturn : number = histData.values[iLast] / histData.values[iStart] - 1;// For complete years, calculate returns from the end of the prior year to the end of the current year. For 'partial years', iStart = 0, or iLast = n - 1

    annualReturns.push({year: yearToSeek, return: annualReturn});
    iStart = iLast; // now, for the inner while loop to work again, we have to swap the indices.
  }

  detailedStats.name = name;
  detailedStats.annualReturns = annualReturns.sort((annualReturn1: AnnualReturn, annualReturn2: AnnualReturn) => annualReturn2.year - annualReturn1.year); // Sort the annualReturns array in descending order. This ensures the latest year data appears first in the list;
  // Calculate the annualized returns for the last 3 and 5 years
  const numOfYears: number = annualReturns.length;
  detailedStats.last3YearsAnnualized = numOfYears >= 3 ? sqAverageOfAnnualReturns(annualReturns, 3) : NaN; // Calculate last 3 years annualized return, if there are at least 3 years of data
  detailedStats.last5YearsAnnualized = numOfYears >= 5 ? sqAverageOfAnnualReturns(annualReturns, 5) : NaN; // Calculate last 5 years annualized return, if there are at least 5 years of data
  return detailedStats;
}

function convertToDateBasedOnDateFormat(date: number, dateFormat: string): Date {
  if (dateFormat == 'YYYYMMDD')
    return parseNumberToDate(date);
  else if (dateFormat.includes('DaysFrom')) {
    const dateStartInd = dateFormat.indexOf('m');
    const dateStartsFrom = parseNumberToDate(parseInt(dateFormat.substring(dateStartInd + 1)));
    return new Date(dateStartsFrom.setDate(dateStartsFrom.getDate() + date));
  } else
    return new Date(date * cSecToMSec);
}