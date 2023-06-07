using System;

namespace QuantConnect.Parameters
{
    public enum SqResult
    {
        QcOriginal, // QC original ResultHandler. Requires a bechmark time series. Too detailed. Creates 6 charts and uses benchmarks for statistics calculation.
        SqPvOnly, // Used in ParameterOptimizer or at other places, where caller will calculate everything (because usually only TotalReturn is needed)
        SqSimple, // Used in PortfolioManager, ChartGenerator. Benchmark is not required. Most importants and quick to calculate stats, TotalReturn, CAGR, StDev, Sharpe, MaxDD is calculated
        SqDetailed // Used in PortfolioViewer. Benchmark is not required. Calculate everything we find useful, e.g. 'Max.TradingDays in DD', that QC doesn't calculate.
    }

    public class SqBacktestConfig
    {
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