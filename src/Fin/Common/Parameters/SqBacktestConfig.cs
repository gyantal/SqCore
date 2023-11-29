using System;
using System.Text;

namespace QuantConnect.Parameters
{
    public enum SqResult
    {
        QcOriginal, // QC original ResultHandler. Requires a bechmark time series. Too detailed. Creates 6 charts and uses benchmarks for statistics calculation.
        SqPvOnly, // Used in ChartGenerator and ParameterOptimizer or at other places, where caller will calculate everything (because usually only TotalReturn is needed)
        SqSimple, // Used in PortfolioManager. Benchmark is not required. Most importants and quick to calculate stats, TotalReturn, CAGR, StDev, Sharpe, MaxDD is calculated
        SqDetailed // Used in PortfolioViewer. Benchmark is not required. Calculate everything we find useful, e.g. 'Max.TradingDays in DD', that QC doesn't calculate.
    }

    public class SqBacktestConfig
    {
        public static bool SqFastestExecution { get; set; } = true; // global variable is fine. Cannot be changed on per strategy level, but being global it can be accessed anywhere from the code
        public static bool SqDailyTradingAtMOC { get; set; } = true; // True: we try to push daily data and trade at 16:00ET. False: QC original: daily data is pushed and trade at 00:00 next day
        public static StringBuilder g_quickDebugLog { get; set; } = new StringBuilder(); // helper for Debugging hard problems
        public bool DoUseIbFeeModelForEquities { get; set; } = false;
        public SqResult SqResult { get; set; } = SqResult.QcOriginal; // Lightweight result calculation, only what SqCore needs, and additional stat numbers that QC doesn't calculate
        public bool DoPeriodicPartialResultsUpdateToCaller { get; set; } = false;
        public bool DoGenerateLog { get; set; } = false;
        public bool DoSaveResultsFiles { get; set; } = false; // enable these for Debugging only, but not in Release, because 110KB file creation is slow

        public float SqInitialDeposit { get; set; }
        public DateTime SqStartDate { get; set; } = DateTime.MinValue;
        public DateTime SqEndDate { get; set; } = DateTime.MinValue;

        public string SqStrategyParams { get; set; } = string.Empty;
    }
}