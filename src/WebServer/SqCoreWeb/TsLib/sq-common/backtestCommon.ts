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
  public sharpeRatio: number = 0;
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

export class UiPfMgrPrtfRunResult extends UiPrtfRunResultCommon { // PrtfRun Results requires position values to display
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
}

export class UiPrtfPositions {
  public sqTicker: string = '';
  public quantity: number = 0;
  public avgPrice: number = 0;
  public price: number = 0;
  public holdingCost: number = 0;
  public holdingValue: number = 0;
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
  public portfolioType = 'Trades'; // default type
  public algorithm = '';
  public algorithmParam = '';
}

export class TreeViewItem { // future work. At the moment, it copies PortfolioFldrJs[] and add the children field. With unnecessary field values. When Portfolios are introduced, this should be rethought.
  // PortfolioItemJs specific fields
  public id = -1;
  public name = '';
  public ownerUserId = -1;
  public parentFolderId = -1;

  public creationTime = ''; // Folder only. not necessary
  public note = ''; // Folder only. not necessary
  public prtfItemType: PrtfItemType = PrtfItemType.Folder;

  // TreeViewItem specific fields
  public children: TreeViewItem[] = []; // children are other TreeViewItems
  public isSelected: boolean = false;
  public isExpanded: boolean = false;

  // Portfolio specific fields
  public baseCurrency = '';
  public type = ''; // Trades or Simulation
  public sharedAccess = '';
  public sharedUserWithMe = '';
  public algorithm = '';
  public algorithmParam = '';
}

export class TreeViewState {
  public lastSelectedItem : Nullable<TreeViewItem> = null;
  public lastSelectedItemId: number = -1; // need to remember the lastselectedItemId to highlight the user Selected item even after refresh or after creating/editing an item
  public expandedPrtfFolderIds: number[] = [];
  // public rootSqTreeViewComponent: Nullable<SqTreeViewComponent> = null;
}
// ************************************************ //
export function preProcessPrtfs(msgObjStr: string ): Nullable<PortfolioJs[]> {
  const prtfs = JSON.parse(msgObjStr, function(this: any, key, value) {
    // eslint-disable-next-line no-invalid-this
    const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

    if (key === 'n') {
      _this.name = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'ouId') {
      _this.ownerUserId = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'p') {
      _this.parentFolderId = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'cTime') {
      _this.creationTime = value;
      return; // if return undefined, original property will be removed
    }

    if (key === 'sAcs') {
      _this.sharedAccess = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'sUsr') {
      _this.sharedUserWithMe = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'bCur') {
      _this.baseCurrency = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'algo') {
      _this.algorithm = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'algoP') {
      _this.algorithmParam = value;
      return; // if return undefined, original property will be removed
    }
    return value;
  });
  return prtfs;
}

export function preProcessFldrs(msgObjStr: string): Nullable<FolderJs[]> {
  const fldrs = JSON.parse(msgObjStr, function(this: any, key, value) {
  // property names and values are transformed to a shorter ones for decreasing internet traffic.Transform them back to normal for better code reading.

    // 'this' is the object containing the property being processed (not the embedding class) as this is a function(), not a '=>', and the property name as a string, the property value as arguments of this function.
    // eslint-disable-next-line no-invalid-this
    const _this: any = this; // use 'this' only once, so we don't have to write 'eslint-disable-next-line' before all lines when 'this' is used

    if (key === 'n') {
      _this.name = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'ouId') {
      _this.ownerUserId = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'p') {
      _this.parentFolderId = value;
      return; // if return undefined, original property will be removed
    }
    if (key === 'cTime') {
      _this.creationTime = value;
      return; // if return undefined, original property will be removed
    }
    return value;
  });
  return fldrs;
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