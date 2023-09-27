using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Parameters;
using QuantConnect.Util;
using MathNet.Numerics.Statistics;
using System.IO;

namespace QuantConnect.Statistics
{
    public static class SqStatisticsBuilder
    {
        public static bool IsTradingDay(DateTime p_date)
        {
            DayOfWeek dayOfWeek = p_date.DayOfWeek;
            return dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday; // not the weekend
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
            // For Debugging: Log out the PV to CSV
            // string csvFilePath = "D:\\Temp\\output.csv";
            // using (StreamWriter writer = new StreamWriter(csvFilePath))
            // {
            //     foreach (ChartPoint point in pointsEquity)
            //     {
            //         string line = $"{Time.UnixTimeStampToDateTime(point.x)},{point.y}";
            //         writer.WriteLine(line);
            //     }
            // }

            StatisticsResults result = new StatisticsResults();

            if (sqResult == SqResult.SqPvOnly)
                return result;
            if (pointsEquity.IsNullOrEmpty()) // if no PVs, then return an empty StatisticsResults
                return result;

            DateTime firstDate = Time.UnixTimeStampToDateTime(pointsEquity[0].x);
            DateTime lastDate = Time.UnixTimeStampToDateTime(pointsEquity[pointsEquity.Count - 1].x);

            // Conceptual decision: QC performance statistics uses the weekends as well
            // We go similar to IB NAV calculation. We skip the weekends, but keep holiday days using the price of the prior day

            // Step 1: determine totalTradingDaysNum
            int totalTradingDaysNum = 0;
            foreach (ChartPoint dailyPV in pointsEquity)
            {
                if (IsTradingDay(Time.UnixTimeStampToDateTime(dailyPV.x)))
                    totalTradingDaysNum += 1;
            }

            // Step 2: calculate histDailyPerf and rolling drawDowns indicators
            DateTime ddStart = firstDate;
            bool isMaxDD = false;
            decimal previousValue = 0m;
            decimal histMaxValue = 0m;
            double histMaxDrawDown = 0;
            int histMaxDDCalLength = 0;
            int ddTradLength = 0;
            int histMaxDDTradLength = 0;
            int histMaxCalDaysBwPeaks = 0;
            int histMaxTradDaysBwPeaks = 0;
            int tradingDayNum = -1;
            double[] histDailyPctChgs = new double[totalTradingDaysNum - 1]; // now we know the size of the array, create it. There are 1 day less daily%change values than the number of days.
            foreach (ChartPoint dailyPV in pointsEquity)
            {
                DateTime currentDate = Time.UnixTimeStampToDateTime(dailyPV.x);
                if (!IsTradingDay(currentDate))
                    continue;

                decimal dailyPValue = dailyPV.y;

                if (dailyPValue >= histMaxValue)
                {
                    int daysInDD = (currentDate - ddStart).Days - 1;
                    histMaxCalDaysBwPeaks = daysInDD > histMaxCalDaysBwPeaks ? daysInDD : histMaxCalDaysBwPeaks;
                    histMaxTradDaysBwPeaks = ddTradLength > histMaxTradDaysBwPeaks ? ddTradLength : histMaxTradDaysBwPeaks;
                    if (isMaxDD)
                    {
                        histMaxDDCalLength = daysInDD;
                        histMaxDDTradLength = ddTradLength;
                        isMaxDD = false;
                    }
                    histMaxValue = dailyPValue;
                    ddStart = currentDate;
                    ddTradLength = -1;
                }

                ddTradLength++;

                if (tradingDayNum == -1) // first day, dailyChange cannot be calculated
                {
                    previousValue = dailyPValue;
                    tradingDayNum++;
                    continue;
                }

                histDailyPctChgs[tradingDayNum] = previousValue > 0 ? (double)((dailyPValue - previousValue) / previousValue) : 0;
                tradingDayNum++;

                double dailyDrawDown = 1 - (double)(dailyPValue / histMaxValue);
                if (dailyDrawDown > histMaxDrawDown)
                {
                    histMaxDrawDown = dailyDrawDown;
                    isMaxDD = true;
                }
                previousValue = dailyPValue;
            }

            // Step 3. Total return and CAGR. Annual compounded returns statistic based on the final-starting capital and years.
            decimal finalCapital = previousValue;
            double histTotalReturn = (double)finalCapital / (double)startingCapital - 1;
            double histCagr = 0;
            double years = (lastDate - firstDate).Days / 365.25;
            if (years != 0 && startingCapital != 0)
            {
                double cagr = Math.Pow(histTotalReturn + 1, 1 / years) - 1; // n-th root of the total return
                histCagr = cagr.IsNaNOrInfinity() ? 0 : cagr;
            }

            // Step 4. AMean, SD, Sharpe, MAR
            double histAMean = ArrayStatistics.Mean(histDailyPctChgs) * 252; // annualized daily mean
            double histSD = ArrayStatistics.StandardDeviation(histDailyPctChgs) * Math.Sqrt(252); // annualized daily StDev, if histDailyPctChgs is empty, StDev becomes NaN, which is correct
            double histSharpe = histSD.IsNaNOrInfinity() ? 0 : histAMean / histSD;
            double histMAR = histMaxDrawDown.IsNaNOrInfinity() ? 0 : histCagr / histMaxDrawDown;

            // Step 5. Writing result dict
            result.Summary[PerformanceMetrics.NetProfit] = (Math.Round(histTotalReturn * 100, 3)).ToStringInvariant() + "%";
            result.Summary[PerformanceMetrics.CompoundingAnnualReturn] = (Math.Round(histCagr * 100, 3)).ToStringInvariant() + "%";
            result.Summary[PerformanceMetrics.AnnualizedMeanReturn] = (Math.Round(histAMean * 100, 3)).ToStringInvariant() + "%";
            result.Summary[PerformanceMetrics.AnnualStandardDeviation] = (Math.Round(histSD, 3)).ToStringInvariant();
            result.Summary[PerformanceMetrics.SharpeRatio] = (Math.Round(histSharpe, 3)).ToStringInvariant();
            result.Summary[PerformanceMetrics.Drawdown] = (Math.Round(histMaxDrawDown * 100, 3)).ToStringInvariant() + "%";
            result.Summary[PerformanceMetrics.MarRatio] = (Math.Round(histMAR, 3)).ToStringInvariant();
            result.Summary[PerformanceMetrics.TotalTrades] = totalTransactions.ToStringInvariant();
            result.Summary[PerformanceMetrics.TotalFees] = accountCurrencySymbol + 0m.ToStringInvariant();
            result.Summary[PerformanceMetrics.MaxDdLenInCalDays] = histMaxDDCalLength.ToStringInvariant();
            result.Summary[PerformanceMetrics.MaxDdLenInTradDays] = histMaxDDTradLength.ToStringInvariant();
            result.Summary[PerformanceMetrics.MaxCalendarDaysBetweenPeaks] = histMaxCalDaysBwPeaks.ToStringInvariant();
            result.Summary[PerformanceMetrics.MaxTradingDaysBetweenPeaks] = histMaxTradDaysBwPeaks.ToStringInvariant();

            return result;
        }
    }
}
