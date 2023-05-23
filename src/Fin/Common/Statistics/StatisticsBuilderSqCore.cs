using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Parameters;
using QuantConnect.Util;
using MathNet.Numerics.Statistics;

namespace QuantConnect.Statistics
{
    public static class SqStatisticsBuilder
    {
        public static bool IsTradingDay(DateTime p_date)
        {
            if (p_date.DayOfWeek == DayOfWeek.Saturday || p_date.DayOfWeek == DayOfWeek.Sunday)
                return false;

            // TODO: consider holidays too if possible
            return true;
        }

        public static StatisticsResults Generate(
            SqResult sqResult,
            List<Trade> trades,
            List<ChartPoint> pointsEquity,
            decimal startingCapital,
            decimal totalFees,
            int totalTransactions,
            string accountCurrencySymbol)
        {
            StatisticsResults result = new StatisticsResults();

            if (sqResult == SqResult.SqPvOnly)
                return result;

            SortedDictionary<DateTime, decimal>? equity = StatisticsBuilder.ChartPointToDictionary(pointsEquity);
            DateTime firstDate = equity.Keys.FirstOrDefault().Date;
            DateTime lastDate = equity.Keys.LastOrDefault().Date;

            List<double> listDailyPerf = new();
            decimal previousValue = 0m;
            decimal histMaxValue = 0m;
            double histMaxDrawDown = 0;
            DateTime ddStart = firstDate;
            DateTime histMaxDDStart = firstDate;
            int histMaxDDCalLength = 0;
            int ddTradLength = 0;
            int histMaxDDTradLength = 0;
            foreach (KeyValuePair<DateTime, decimal> dailyPV in equity)
            {
                if (previousValue != 0)
                {
                    ddTradLength++;
                    DateTime currentDate = dailyPV.Key.Date;
                    decimal dailyPValue = dailyPV.Value;
                    if (dailyPValue >= histMaxValue)
                    {
                        histMaxValue = dailyPValue;
                        ddStart = currentDate;
                        ddTradLength = 0;
                    }
                    decimal dailyChange = dailyPValue - previousValue;
                    double dailyPercentageChange = (double)(dailyChange / previousValue);
                    double dailyDrawDown = 1 - (double)(dailyPValue / histMaxValue);
                    int daysInDD = (currentDate - ddStart).Days;

                    listDailyPerf.Add(dailyPercentageChange);
                    if (dailyDrawDown > histMaxDrawDown)
                    {
                        histMaxDrawDown = dailyDrawDown;
                        histMaxDDStart = ddStart;
                        histMaxDDCalLength = daysInDD;
                        histMaxDDTradLength = ddTradLength;
                    }
                }

                previousValue = dailyPV.Value;
            }

            decimal finalCapital = equity.Values.LastOrDefault(); // or previousValue
            // Total return
            double histTotalReturn = (double)finalCapital / (double)startingCapital - 1;

            // CAGR. Annual compounded returns statistic based on the final-starting capital and years.
            double histCagr = 0;
            double years = (lastDate - firstDate).Days / 365.25;
            if (years != 0 && startingCapital != 0)
            {
                double cagr = Math.Pow(histTotalReturn + 1, 1 / years) - 1; // n-th root of the total return
                histCagr = cagr.IsNaNOrInfinity() ? 0 : cagr;
            }

            // AMean, SD, Sharpe, MAR
            double[]? histDailyPerf = listDailyPerf.ToArray();
            double histAMean = ArrayStatistics.Mean(histDailyPerf) * 252;
            double histSD = ArrayStatistics.StandardDeviation(histDailyPerf) * Math.Sqrt(252);
            double histSharpe = histSD.IsNaNOrInfinity() ? 0 : histAMean / histSD;
            double histMAR = histMaxDrawDown.IsNaNOrInfinity() ? 0 : histCagr / histMaxDrawDown;

            // result.Summary
            return result;
        }
    }
}
