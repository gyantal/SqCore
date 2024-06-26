namespace QuantConnect.Statistics
{
    /// <summary>
    /// PerformanceMetrics contains the names of the various performance metrics used for evaluation purposes.
    /// </summary>
    public static class PerformanceMetrics
    {
        public const string Alpha = "Alpha";
        public const string AnnualStandardDeviation = "Annual Standard Deviation";
        public const string AnnualVariance = "Annual Variance";
        public const string AverageLoss = "Average Loss";
        public const string AverageWin = "Average Win";
        public const string Beta = "Beta";
        public const string CompoundingAnnualReturn = "Compounding Annual Return";
        public const string Drawdown = "Drawdown";
        public const string EstimatedStrategyCapacity = "Estimated Strategy Capacity";
        public const string Expectancy = "Expectancy";
        public const string InformationRatio = "Information Ratio";
        public const string LossRate = "Loss Rate";
        public const string NetProfit = "Net Profit";
        public const string ProbabilisticSharpeRatio = "Probabilistic Sharpe Ratio";
        public const string ProfitLossRatio = "Profit-Loss Ratio";
        public const string SharpeRatio = "Sharpe Ratio";
        public const string TotalFees = "Total Fees";
        public const string TotalTrades = "Total Trades";
        public const string TrackingError = "Tracking Error";
        public const string TreynorRatio = "Treynor Ratio";
        public const string WinRate = "Win Rate";
        public const string LowestCapacityAsset = "Lowest Capacity Asset";
        // SqCore Change NEW:
        public const string AnnualizedDailyMeanReturn = "Annualized Daily Mean Return";
        public const string CagrSharpeRatio = "CAGR Sharpe Ratio"; // standard Sharpe ratio uses AnnualizedDailyMeanReturn/StDev = DailyReturn/DailyStDev. (this is daily returns added together 252x, not sensitive to leverage. TQQQ Sharpe will be similar to QQQ Sharpe) We would like to see CAGR/StDev (CAGR is multiplicative compounding, TQQQ SqSharpe will be lower than QQQ SqSharpe)
        public const string MarRatio = "MAR Ratio";
        public const string MaxDdLenInCalDays = "Max Drawdown Lenght in Calendar Days";
        public const string MaxDdLenInTradDays = "Max Drawdown Lenght in Trading Days";
        public const string MaxCalendarDaysBetweenPeaks = "Max Calendar Days Between Peaks";
        public const string MaxTradingDaysBetweenPeaks = "Max Trading Days Between Peaks";
        // SqCore Change END
    }
}
