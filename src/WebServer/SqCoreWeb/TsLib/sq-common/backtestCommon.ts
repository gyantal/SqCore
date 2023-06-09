// ************************************************ //
// Classes used for developing charts, stats and positions of PortfolioRunResults
// The below classes are used in PortfolioManager and ChartGenerator Apps

export enum ChartResolution
{
    Second, Minute, Minute5, Hour, Daily, Weekly, Monthly
}

export enum SqLogLevel
{
    Off, Trace, Debug, Info, Warn, Error, Fatal
}

export interface ChartJs { // PfRunResults Chart Data
  dates: number[];
  values: number[];
  chartResolution: ChartResolution;
}

export class SqLog {
  public sqLogLevel = '';
  public message = '';
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
  public prtfName!: string;
}

export class PrtfRunResultJs extends ChrtGenPfRunResult { // we can specify the input types more, but whatever.
  public prtfPoss: any; // all the position members from UiPrtfPositions, we skip creating detailed sub classes
}


// Ui classes
export class UiChrtGenPrtfRunResult {
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
  public chrtValues: UiChartPointValue[] = []; // used in PrtfRunResults in portfolioManager app

  public prtfChrtValues: UiChrtGenValue[] = []; // used in backtestResults in chrtGen app
  public bmrkChrtValues: UiChrtGenValue[] = []; // used in backtestResults in chrtGen app
  public sqLogs: SqLog[] = [];
}

export class UiPrtfRunResult extends UiChrtGenPrtfRunResult { // PrtfRun Results requires position values to display
  public prtfPosValues: UiPrtfPositions[] = [];
}

// chart values
export class UiChartPointValue {
  public date = new Date();
  public value = NaN;
}

export class UiChrtGenValue extends UiChartPointValue {
  public name: string = '';
  public chartResolution: string = '';
  public priceData: UiChartPointValue[] = [];
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