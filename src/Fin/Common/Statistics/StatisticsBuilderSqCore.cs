using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Parameters;
using QuantConnect.Util;

namespace QuantConnect.Statistics
{
    public static class SqStatisticsBuilder
    {
        public static StatisticsResults Generate(
            SqResult sqResult,
            List<Trade> trades,
            List<ChartPoint> pointsEquity,
            decimal startingCapital,
            decimal totalFees,
            int totalTransactions,
            string accountCurrencySymbol)
        {
            if (sqResult == SqResult.SqPvOnly)
                new StatisticsResults();
            // var equity = ChartPointToDictionary(pointsEquity);

            // var firstDate = equity.Keys.FirstOrDefault().Date;
            // var lastDate = equity.Keys.LastOrDefault().Date;

            // var totalPerformance = GetAlgorithmPerformance(firstDate, lastDate, trades, profitLoss, equity, pointsPerformance, pointsBenchmark, startingCapital);
            // var rollingPerformances = GetRollingPerformances(firstDate, lastDate, trades, profitLoss, equity, pointsPerformance, pointsBenchmark, startingCapital);
            // var summary = GetSummary(totalPerformance, estimatedStrategyCapacity, totalFees, totalTransactions, accountCurrencySymbol);

            // return new StatisticsResults(totalPerformance, rollingPerformances, summary);
            return new StatisticsResults();
        }
    }
}
