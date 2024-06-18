using System;
using System.Text;

namespace QuantConnect.Parameters
{
    public enum SqResultStat
    {
        NoStat, // Used in ChartGenerator and ParameterOptimizer or at other places, where caller will calculate everything (because usually only TotalReturn is needed)
        QcOriginalStat, // QC original ResultHandler. Requires a bechmark time series. Too detailed. Creates 6 charts and uses benchmarks for statistics calculation.
        SqSimpleStat, // Used in PortfolioManager. Benchmark is not required. Most importants and quick to calculate stats, TotalReturn, CAGR, StDev, Sharpe, MaxDD is calculated
        SqDetailedStat // Used in PortfolioViewer. Benchmark is not required. Calculate everything we find useful, e.g. 'Max.TradingDays in DD', that QC doesn't calculate.
    }

    public class SqBacktestConfig
    {
        public static bool SqFastestExecution { get; set; } = true; // global variable is fine. Cannot be changed on per strategy level, but being global it can be accessed anywhere from the code
        public static bool SqDailyTradingAtMOC { get; set; } = true; // True: we try to push daily data and trade at 16:00ET. False: QC original: daily data is pushed and trade at 00:00 next day
        public static StringBuilder g_quickDebugLog { get; set; } = new StringBuilder(); // helper for Debugging hard problems
        public bool DoUseIbFeeModelForEquities { get; set; } = false;

        // Sampling refers that the BacktestingResultHandler.ProcessSynchronousEvents() run periodically (daily) in the main loop and creates the result 'Charts' our our favoured RawPv or TwrPv lists.
        public bool SamplingQcOriginal { get; set; } = false; // this generates the usual QC Charts.

        // We start TwrPV from 100.0 (not 1.0 and not 0.0). We treat it as value-less. Don't force it as it is a % or $.
        // The financial literature sometimes starts a TWR chart from 0, sometimes from 1.0
        // IntBrokers PortfolioAnalyst starts TRW chart from 0%. YF stock comparing tool also starts from 0%.
        // Step1: However, we intentionally choose the 1. It is a choice. Reason1: If my PV went from 1.0 to 3.0 over 5 years, I can easily understand that it is a triple. (the same as going from 0% to 200% doesn't give this feeling)
        // Reason2: If we start from 0 and use percentage change, calculating drawdowns becomes less straightforward. Let's assume we are at a 400% profit when it drops to 200%. Based on 'the % TWR chart starting from 0%', one might think that the DD is 50%. However, in reality, it fell from 500 to 300, which is only a 40% DD.
        // Step2: Instead 1.0, we prefer 100.0, because numerically it is better to send data as "97.12" instead "0.9712". It is also better if we debug a backtest. It is also better for the user to see it on the UI. Easier cognitive load.
        public bool SamplingSqTwrPv { get; set; } = true; // by default we only need the TWR PV (not the rawPV) for calculating proper maxDD, Sharpe etc.
        public bool SamplingSqRawPv { get; set; } = true;

        public SqResultStat SqResultStat { get; set; } = SqResultStat.SqSimpleStat; // Lightweight result calculation, only what SqCore needs, and additional stat numbers that QC doesn't calculate

        public bool DoPeriodicPartialResultsUpdateToCaller { get; set; } = false;
        public bool DoGenerateLog { get; set; } = false;
        public bool DoSaveResultsFiles { get; set; } = false; // enable these for Debugging only, but not in Release, because 110KB file creation is slow
    }
}