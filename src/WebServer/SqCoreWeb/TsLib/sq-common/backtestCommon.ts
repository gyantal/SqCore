// ************************************************ //
// Classes used for developing charts, stats and positions of PortfolioRunResults
// The below classes are used in PortfolioManager and ChartGenerator Apps
// Input data classes

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
// ************************************************ //