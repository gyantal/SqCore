using System;

namespace QuantConnect.Parameters
{
    public enum SqResult
    {
        QcOriginal, // QC original ResultHandler. Too detailed. Creates 6 charts and uses benchmarks for statistics calculation.
        SqPvOnly, // Used in ChartGenerator, where UI Client will calculate everything
        SqSimple, // Used in PortfolioManager. Most importants and quick to calculate stats, TotalReturn, CAGR, StDev, Sharpe, MaxDD is calculated
        SqDetailed // Used in PortfolioViewer. Calculate everything we find useful, e.g. 'Max.TradingDays in DD', that QC doesn't calculate.
    }

    public class SqBacktestConfig
    {
        public bool DoUseIbFeeModelForEquities { get; set; } = false;
        public SqResult SqResult { get; set; } = SqResult.QcOriginal; // Lightweight result calculation, only what SqCore needs, and additional stat numbers that QC doesn't calculate
        public bool DoPeriodicPartialResultsUpdateToCaller { get; set; } = false;
        public bool DoGenerateLog { get; set; } = false;

        public float SqInitialDeposit { get; set; }
        public DateTime SqStartDate { get; set; } = DateTime.MinValue;
        public DateTime SqEndDate { get; set; } = DateTime.MinValue;

        public string SqStrategyParams { get; set; } = string.Empty;
    }
}