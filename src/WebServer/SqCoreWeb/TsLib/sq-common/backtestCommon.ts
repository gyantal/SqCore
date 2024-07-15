import * as d3 from 'd3';
import { prtfRunResultChrt } from '../sq-common/chartAdvanced';

// ************************************************ //
// Classes used for developing charts, stats and positions of PortfolioRunResults
// The below classes are used in PortfolioManager and ChartGenerator Apps

// Input data classes
type Nullable<T> = T | null;

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
  public price: number = 0;
  public holdingCost: number = 0;
  public holdingValue: number = 0;
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
    posItem.price = prtfRunResult.prtfPoss[i].lastPrice;
    posItem.holdingCost = posItem.avgPrice * posItem.quantity;
    posItem.holdingValue = posItem.price * posItem.quantity;
    uiPrtfRunResult.prtfPosValues.push(posItem);
  }

  uiPrtfRunResult.chrtValues.length = 0;
  for (let i = 0; i < prtfRunResult.chrtData.dates.length; i++) {
    const chartItem = new UiChartPoint();
    const mSecSinceUnixEpoch: number = prtfRunResult.chrtData.dates[i] * 1000; // data comes as seconds. JS uses milliseconds since Epoch.
    chartItem.date = new Date(mSecSinceUnixEpoch);
    chartItem.value = prtfRunResult.chrtData.values[i];
    uiPrtfRunResult.chrtValues.push(chartItem);
  }

  d3.selectAll('#pfRunResultChrt > *').remove();
  const lineChrtDiv = document.getElementById('pfRunResultChrt') as HTMLElement;
  const margin = {top: 50, right: 50, bottom: 30, left: 60 };
  const chartWidth = uiChrtWidth * 0.9 - margin.left - margin.right; // 90% of the PanelChart Width
  const chartHeight = uiChrtHeight * 0.9 - margin.top - margin.bottom; // 90% of the PanelChart Height
  const chrtData = uiPrtfRunResult.chrtValues.map((r:{ date: Date; value: number; }) => ({date: new Date(r.date), value: r.value}));
  const xMin = d3.min(chrtData, (r:{ date: Date; }) => r.date);
  const xMax = d3.max(chrtData, (r:{ date: Date; }) => r.date);
  const yMinAxis = d3.min(chrtData, (r:{ value: number; }) => r.value);
  const yMaxAxis = d3.max(chrtData, (r:{ value: number; }) => r.value);

  prtfRunResultChrt(chrtData, lineChrtDiv, chartWidth, chartHeight, margin, xMin, xMax, yMinAxis, yMaxAxis);
}