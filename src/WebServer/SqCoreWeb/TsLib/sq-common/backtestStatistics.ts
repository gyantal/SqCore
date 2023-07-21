import { CgTimeSeries } from '../sq-common/backtestCommon';
// import { lbaverage, lbstdDev } from '../sq-common/utils_math';

export class StatisticsResults {
  public TotalReturn: number = 0;
  public CAGR: number = 0;
  // public AnnualizedMeanReturn: number = 0;
  // public SharpeRatio: number = 0;
  // public Drawdown: number = 0;
  // public MarRatio: number = 0;
  // public MaxDdLenInCalDays: number = 0;
  // public MaxDdLenInTradDays: number = 0;
  // public TotalTrades: number = 0;
  // public TotalFees: number = 0;
}

export class FinalStatistics {
  public name: string = '';
  public stats: StatisticsResults = new StatisticsResults();
}

export class SqStatisticsBuilder {
  _chartData: CgTimeSeries[] = [];

  public Init(chartData: CgTimeSeries[]): void {
    this._chartData = chartData;
  }

  isTradingDay(startDate: Date): boolean {
    const dayOfWeek = startDate.getDay();
    return dayOfWeek !== 6 && dayOfWeek !== 0; // not the weekend
  }

  public statsResults(startDate: Date, endDate: Date): FinalStatistics[] { // without the dataCopy
    const statsResults: FinalStatistics[] = [];
    if (this._chartData == null)
      return statsResults;
    let startingCapital: number = 0;
    let finalCapital: number = 0;
    for (const data of this._chartData) {
      const statRes = new FinalStatistics();
      statRes.name = data.name;
      for (let i = 0; i < data.priceData.length; i++) {
        const date = new Date(data.priceData[i].date);
        if (date >= startDate && date <= endDate) {
          startingCapital = data.priceData[i].value;
          finalCapital = data.priceData[data.priceData.length - 1].value;
          statRes.stats.TotalReturn = finalCapital / startingCapital - 1;
          break;
        }
      }
      const years = (endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24 * 365.25);
      if (years !== 0 && startingCapital !== 0) {
        const cagr = Math.pow( statRes.stats.TotalReturn + 1, 1 / years) - 1; // n-th root of the total return
        statRes.stats.CAGR = isNaN(cagr) || !isFinite(cagr) ? 0 : cagr;
      }
      statsResults.push(statRes);
    }
    return statsResults;
  }

  // public statsResults_v0(startDate: Date, endDate: Date): StatisticsResults[] { // Old Method version.0(v0)
  //   const statsRes: StatisticsResults[] = [];
  //   if (this._chartData == null)
  //     return statsRes;

  //   // Step 0: Slicing the data
  //   const slicedChartData: CgTimeSeries[] = [];
  //   for (const data of this._chartData) {
  //     const slicedData: UiChartPoint[] = [];
  //     for (let i = 0; i < data.priceData.length; i++) {
  //       const chrtdata = data.priceData[i];
  //       const date = new Date(chrtdata.date);

  //       if (date >= startDate && date <= endDate)
  //         slicedData.push(chrtdata);
  //     }

  //     if (slicedData.length > 0) {
  //       const newSlicedData: UiChartPoint[] = [];
  //       for (let i = 0; i < slicedData.length; i++) {
  //         const chrtPointVal = new UiChartPoint();
  //         chrtPointVal.date = slicedData[i].date;
  //         chrtPointVal.value = 100 * slicedData[i].value / slicedData[0].value;
  //         newSlicedData.push(chrtPointVal);
  //       }
  //       const dataCopy: CgTimeSeries = { name: data.name, chartResolution: data.chartResolution, priceData: newSlicedData };
  //       slicedChartData.push(dataCopy);
  //     }
  //   }

  //   // Step 1: determine totalTradingDaysNum
  //   let totalTradingDaysNum = 0;
  //   for (const item of slicedChartData) {
  //     const res = new StatisticsResults();
  //     const firstDate: Date = item.priceData[0].date;
  //     const lastDate = item.priceData[item.priceData.length - 1].date;
  //     const startingCapital = item.priceData[0].value;
  //     for (const dailyPV of item.priceData) {
  //       if (this.isTradingDay(dailyPV.date))
  //         totalTradingDaysNum += 1;
  //     }

  //     // Step 2: calculate histDailyPerf and rolling drawDowns indicators
  //     let ddStart = firstDate;
  //     let isMaxDD = false;
  //     let previousValue = 0;
  //     let histMaxValue = 0;
  //     let histMaxDrawDown = 0;
  //     let histMaxDDCalLength = 0;
  //     let ddTradLength = 0;
  //     let histMaxDDTradLength = 0;
  //     let histMaxCalDaysBwPeaks = 0;
  //     let histMaxTradDaysBwPeaks = 0;
  //     let tradingDayNum = -1;
  //     const histDailyPctChgs: number[] = new Array<number>(totalTradingDaysNum - 1); // now we know the size of the array, create it. There are 1 day less daily%change values than the number of days.
  //     for (const dailyPV of item.priceData) {
  //       const currentDate = dailyPV.date;
  //       if (!this.isTradingDay(currentDate))
  //         continue;


  //       const dailyPValue = dailyPV.value;

  //       if (dailyPValue >= histMaxValue) {
  //         const daysInDD = Math.floor((currentDate.getTime() - ddStart.getTime()) / (1000 * 60 * 60 * 24)) - 1;
  //         histMaxCalDaysBwPeaks = daysInDD > histMaxCalDaysBwPeaks ? daysInDD : histMaxCalDaysBwPeaks;
  //         histMaxTradDaysBwPeaks = ddTradLength > histMaxTradDaysBwPeaks ? ddTradLength : histMaxTradDaysBwPeaks;
  //         if (isMaxDD) {
  //           histMaxDDCalLength = daysInDD;
  //           histMaxDDTradLength = ddTradLength;
  //           isMaxDD = false;
  //         }
  //         histMaxValue = dailyPValue;
  //         ddStart = currentDate;
  //         ddTradLength = -1;
  //       }

  //       ddTradLength++;

  //       if (tradingDayNum === -1) { // first day, dailyChange cannot be calculated
  //         previousValue = dailyPValue;
  //         tradingDayNum++;
  //         continue;
  //       }

  //       histDailyPctChgs[tradingDayNum] = previousValue > 0 ? (dailyPValue - previousValue) / previousValue : 0;
  //       tradingDayNum++;

  //       const dailyDrawDown = 1 - (dailyPValue / histMaxValue);
  //       if (dailyDrawDown > histMaxDrawDown) {
  //         histMaxDrawDown = dailyDrawDown;
  //         isMaxDD = true;
  //       }
  //       previousValue = dailyPValue;
  //     }

  //     // Step 3. Total return and CAGR. Annual compounded returns statistic based on the final-starting capital and years.
  //     const finalCapital = previousValue;
  //     const histTotalReturn = finalCapital / startingCapital - 1;
  //     let histCagr = 0;
  //     const years = (lastDate.getTime() - firstDate.getTime()) / (1000 * 60 * 60 * 24 * 365.25);
  //     if (years !== 0 && startingCapital !== 0) {
  //       const cagr = Math.pow(histTotalReturn + 1, 1 / years) - 1; // n-th root of the total return
  //       histCagr = isNaN(cagr) || !isFinite(cagr) ? 0 : cagr;
  //     }

  //     // Step 4. AMean, SD, Sharpe, MAR
  //     const histAMean = lbaverage(histDailyPctChgs) * 252;
  //     const histSD = lbstdDev(histDailyPctChgs) * Math.sqrt(252);
  //     const histSharpe = isNaN(histSD) || !isFinite(histSD) ? 0 : histAMean / histSD;
  //     const histMAR = isNaN(histMaxDrawDown) || !isFinite(histMaxDrawDown) ? 0 : histCagr / histMaxDrawDown;

  //     // Step 5. Writing result
  //     res.AnnualizedMeanReturn = (histAMean * 100 * 1000) / 1000;
  //     res.CAGR = (histCagr * 100 * 1000) / 1000;
  //     res.SharpeRatio = (histSharpe * 1000) / 1000;
  //     res.Drawdown = (histMaxDrawDown * 100 * 1000) / 1000;
  //     res.MarRatio = (histMAR * 1000) / 1000;
  //     res.MaxDdLenInCalDays = histMaxDDCalLength;
  //     res.MaxDdLenInTradDays = histMaxDDTradLength;
  //     res.TotalReturn = item.priceData[0].value / item.priceData[item.priceData.length - 1].value;
  //     // res.TotalTrades = this._totalTransactions;
  //     // res.TotalFees = this._accountCurrencySymbol;
  //     statsRes.push(res);
  //   }
  //   return statsRes;
  // }
}