using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Fin.Base;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;
using QuantConnect.Parameters;
using QuantConnect.Securities;
using QuantConnect.Statistics;
using QuantConnect.Util;
using SqCommon;

namespace Fin.MemDb;

public class PortfolioRunResultStatistics
{
    [JsonPropertyName("startPv")]
    public float StartPortfolioValue { get; set; } = 0.0f;
    [JsonPropertyName("endPv")]
    public float EndPortfolioValue { get; set; } = 0.0f;
    [JsonPropertyName("tr")]
    public float TotalReturn { get; set; } = 0.0f;
    public float CAGR { get; set; } = 0.0f;
    public float MaxDD { get; set; } = 0.0f;
    [JsonPropertyName("shrp")]
    [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
    public float Sharpe { get; set; } = 0.0f;
    [JsonPropertyName("cagrShrp")]
    [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
    public float CagrSharpe { get; set; } = 0.0f;
    public float StDev { get; set; } = 0.0f;
    public float Ulcer { get; set; } = 0.0f;
    public int TradingDays { get; set; } = 0;
    public int NTrades { get; set; } = 0; // number of trades
    [JsonPropertyName("wr")]
    public float WinRate { get; set; } = 0.0f;
    [JsonPropertyName("lr")]
    public float LossRate { get; set; } = 0.0f;
    [JsonPropertyName("srtn")]
    [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
    public float Sortino { get; set; } = 0;
    [JsonPropertyName("to")]
    public float Turnover { get; set; } = 0.0f;
    [JsonPropertyName("ls")]
    public float LongShortRatio { get; set; } = 0.0f;
    public float Fees { get; set; } = 0.0f;
    [JsonPropertyName("bCAGR")]
    public float BenchmarkCAGR { get; set; } = 0.0f;
    [JsonPropertyName("bMax")]
    public float BenchmarkMaxDD { get; set; } = 0.0f;
    [JsonPropertyName("cwb")]
    public float CorrelationWithBenchmark { get; set; } = 0.0f;
}

public enum ChartResolution
{
    Second, Minute, Minute5, Hour, Daily, Weekly, Monthly
}

public enum DateTimeFormat // "SecSince1970", "YYYYMMDD", "DaysFrom<YYYYDDMM>"
{
    Unknown, SecSince1970, YYYYMMDD, DaysFromADate
}

public class PortfolioPosition
{
    public string SqTicker { get; set; } = string.Empty;
    public float Quantity { get; set; } = float.NaN; // int quantity is not good because fractional Crypto tokens or fractional AAPL shares can be traded
    public float AvgPrice { get; set; } = 0.0f;
    public float BacktestLastPrice { get; set; } = 0.0f;  // the last price of the asset at the end of the backtest (not real-time price)
    public float EstPrice { get; set; } = 0.0f;  // MktValue can be calculated (real-time price)
}

public class PriceHistoryJs // To save bandwidth, we send Dates, and Prices just as a List, instead of a List of <Date,Price> objects that would add property names thousands of times into JSON
{
    public List<uint> Dates { get; set; } = new(); // UInt32 is enough if time is represented as seconds since 1970. Would only cover until 2106. (uint32 max. 4,294,967,295 seconds that is 132 years)

    [JsonConverter(typeof(FloatListJsonConverterToNumber4D))]
    public List<float> Prices { get; set; } = new();
}

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public partial class Portfolio : Asset // this inheritance makes it possible that a Portfolio can be part of an Uber-portfolio
{
    public virtual List<Base.Trade>? GetTradeHistory()
    {
        return MemDb.gMemDb.GetPortfolioTradeHistoryToList(this.TradeHistoryId, null, null); // Don't filter TradeHist based on StartDate, because to properly backtest we need the initial trades that happende Before StartDate. StartDate refers to the ChartGeneration usually. But we have to simulate previous buying trades, even before StartDate.
    }

    // PortfolioValue chart data.
    // We have the option to return Date fields in different formats in JSON string:
    // '2021-01-27' is 10 chars, '20210127' is 8 chars. Resolution is only daily.
    // Or number of seconds from Unix epoch: '1641013200' is 10 chars. Resolution can be 1 second.
    // Although it is 2 chars more data, but we chose this, because QC uses it and also it will allow us to go intraday in the future.
    // Also it allows to show the user how up-to-date (real-time) the today value is.
    public string? GetPortfolioRunResult(bool p_returnOnlyTwrPv, SqResultStat p_sqResultStat, DateTime? p_forcedStartTimeUtc, DateTime? p_forcedEndTimeUtc, out PortfolioRunResultStatistics p_stat, out List<DateValue> p_pv, out List<PortfolioPosition> p_prtfPoss, out ChartResolution p_chartResolution, out List<SqLog> p_sqLogs)
    {
        // "Trades" type portfolios can have Trades in them for the past (for that "SqTradeAccumulation" Algorithm has to be run now),
        // but a different Algorithm ("CXO", "HL") might exist in the Portfolio.Algorithm, that will be run for the future, e.g. VBroker to determine what to trade at the broker. Or in the very far past (before the era of the trades)
        // So, for Trades, and LegacyDbTrades portfolios don't use the stored this.Algorithm.

        string algorithmName = (this.Type == PortfolioType.Trades || this.Type == PortfolioType.LegacyDbTrades) ? "SqTradeAccumulation" : this.Algorithm;
        if (String.IsNullOrEmpty(algorithmName)) // if there is an Algorithm, we can run it. LegacyDbTrades uses 'SqTradeAccumulation' algorithm
            return GetPortfolioRunResultDefault(out p_stat, out p_pv, out p_prtfPoss, out p_chartResolution, out p_sqLogs);
        else
            return GetBacktestResult(algorithmName, p_returnOnlyTwrPv, p_sqResultStat, p_forcedStartTimeUtc, p_forcedEndTimeUtc, out p_pv, out p_stat, out p_prtfPoss, out p_chartResolution, out p_sqLogs);

        // #pragma warning disable IDE0066 // disable the switch suggestion warning only locally
        // switch (Type)
        // {
        //     case PortfolioType.Simulation:
        //         return GetBacktestResult(p_returnOnlyTwrPv, p_sqResultStat, p_forcedStartDate, p_forcedEndDate, out p_pv,  out p_stat, out p_prtfPoss, out p_chartResolution);
        //     case PortfolioType.Trades:
        //     case PortfolioType.LegacyDbTrades:
        //     default:
        //         return GetPortfolioRunResultDefault(out p_stat, out p_pv, out p_prtfPoss, out p_chartResolution);
        // }
        // #pragma warning restore IDE0066
    }

    public static string? GetPortfolioRunResultDefault(out PortfolioRunResultStatistics p_stat, out List<DateValue> p_pv, out List<PortfolioPosition> p_prtfPoss, out ChartResolution p_chartResolution, out List<SqLog> p_sqLogs)
    {
        List<DateValue> pvs = new()
        {
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641013200).DateTime, Value = 101665f }, // DateTimeOffset.FromUnixTimeSeconds(pv[0].DateInLong).DateTime.Date;
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641099600).DateTime, Value = 101487f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641186000).DateTime, Value = 101380f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641272400).DateTime, Value = 101451f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641358800).DateTime, Value = 101469f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641445200).DateTime, Value = 101481f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641531600).DateTime, Value = 101535f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641618000).DateTime, Value = 101416f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641704400).DateTime, Value = 101392f },
            new DateValue { Date = DateTimeOffset.FromUnixTimeSeconds(1641790800).DateTime, Value = 101386f }
        }; // 5 or 10 real values.

        p_pv = pvs; // output
        p_stat = new PortfolioRunResultStatistics
        {
            StartPortfolioValue = 1000.0f,
            EndPortfolioValue = 1400.0f,
            Sharpe = 0.8f
        }; // output
        List<PortfolioPosition> prtfPoss = new()
        {
            new PortfolioPosition { SqTicker = "S/SPY", Quantity = 1.0f, AvgPrice = 1.0f, BacktestLastPrice = 1.0f },
            new PortfolioPosition { SqTicker = "S/TQQQ", Quantity = 1.0f, AvgPrice = 1.0f, BacktestLastPrice = 1.0f }
        }; // output
        p_prtfPoss = prtfPoss;
        p_chartResolution = ChartResolution.Daily;
        List<SqLog> sqLogs = new();
        p_sqLogs = sqLogs;
        return null; // No Error
    }

    public string? GetBacktestResult(string p_algorithmName, bool p_returnOnlyTwrPv, SqResultStat p_sqResultStat, DateTime? p_forcedStartTimeUtc, DateTime? p_forcedEndTimeUtc, out List<DateValue> p_pv, out PortfolioRunResultStatistics p_stat, out List<PortfolioPosition> p_prtfPoss, out ChartResolution p_chartResolution, out List<SqLog> p_sqLogs)
    {
        SqBacktestConfig backtestConfig = new SqBacktestConfig() { SqResultStat = p_sqResultStat, SamplingQcOriginal = false, SamplingSqDailyRawPv = false, SamplingSqDailyTwrPv = false };
        if (p_returnOnlyTwrPv) // target is TwrPv
            backtestConfig.SamplingSqDailyTwrPv = true;
        else // target is RawPV
        {
            backtestConfig.SamplingSqDailyRawPv = true;
            if (p_sqResultStat == SqResultStat.SqSimpleStat || p_sqResultStat == SqResultStat.SqDetailedStat) // return Raw PV, but we also also have to generate TwrPV, because of SqSimple and SqDetailed calculations use TWR
                backtestConfig.SamplingSqDailyTwrPv = true;
        }

        if (p_algorithmName == "BasicTemplateFrameworkAlgorithm") // If Original QC algorithms, usually per minute.
        {
            backtestConfig.SamplingQcOriginal = true; // QC supports per minute resolution
            backtestConfig.SamplingSqDailyRawPv = false; // our Sampling can do only Daily resolution
            backtestConfig.SamplingSqDailyTwrPv = false;
            backtestConfig.SqResultStat = SqResultStat.QcOriginalStat; // our QcSimple, QcDetailed can only work with Daily resolution TwrPV
        }

        p_pv = new List<DateValue>();
        p_stat = new PortfolioRunResultStatistics();
        p_prtfPoss = new List<PortfolioPosition>();
        p_chartResolution = ChartResolution.Daily;
        p_sqLogs = new List<SqLog>();

        string backtestAlgorithmParam = GetBacktestAlgorithmParam(p_forcedStartTimeUtc, p_forcedEndTimeUtc, AlgorithmParam); // AlgorithmParam itself 'can' have StartDate, EndDate. But ChartGenerator can further restricts the period with forcedStartDate/EndDate
        List<Base.Trade>? portTradeHist = this.GetTradeHistory(); // Don't filter TradeHist based on StartDate, because to properly backtest we need the initial trades that happend Before StartDate. StartDate refers to the ChartGeneration usually. But we have to simulate previous buying trades, even before StartDate.
        BacktestingResultHandler backtestResults = Backtester.BacktestInSeparateThreadWithTimeout(p_algorithmName, backtestAlgorithmParam, portTradeHist, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
        if (backtestResults == null)
            return "Error in Backtest";

        backtestResults.LogStore.ForEach(r => Console.WriteLine(r.Message)); // Trade Logs. "Time: 10/07/2013 13:31:00 OrderID: 1 EventID: 2 Symbol: SPY Status: Filled Quantity: 688 FillQuantity: 688 FillPrice: 144.7817 USD OrderFee: 3.44 USD"
        DateTime btResultStartDate = DateTime.MinValue;
        DateTime btResultEndDate = DateTime.MinValue;
        if (backtestConfig.SamplingSqDailyTwrPv)
        {
            btResultStartDate = backtestResults.SqSampledLists["twrPV"][0].Date;
            btResultEndDate = backtestResults.SqSampledLists["twrPV"][^1].Date;
        }
        Console.WriteLine($"BacktestResults. btResultStartDate:{btResultStartDate}, btResultEndDate:{btResultEndDate}");
        decimal returnPct = backtestResults.StartingPortfolioValue == 0 ? 0 : (backtestResults.DailyPortfolioValue / backtestResults.StartingPortfolioValue - 1) * 100;
        Console.WriteLine($"BacktestResults.PV. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0} (If noDeposit: {returnPct:N2}%)");

        // Step 0: Fill the logs.
        foreach(Packet? msg in backtestResults.Messages)
        {
            if (msg == null)
                continue;
            bool isExpectedMessage = msg is SecurityTypesPacket || msg is DebugPacket || msg is LogPacket || msg is SystemDebugPacket;
            // The base class of packets does not contain a 'Message' property.
            // Only the derived classes (DebugPacket, LogPacket, AlgorithmStatusPacket, and HandledErrorPacket) include the 'Message' member.
            // Since we are already filtering DebugPacket and LogPacket, we cast 'msg' to HandledErrorPacket to access its 'Message' property.
            if (!isExpectedMessage)
            {
                if (msg is HandledErrorPacket errorPacket)
                    p_sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Error, Message = errorPacket.Message });
                else
                    p_sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Warn, Message = $"QcBacktest unrecognized Msg: '{msg.GetType()}'. Add necessary handling to the code!" });
            }
        }

        // Step 1: create the p_pv of the result.
        if (backtestConfig.SamplingSqDailyRawPv || backtestConfig.SamplingSqDailyTwrPv) // ChartResolution.Daily
            p_pv = backtestConfig.SamplingSqDailyTwrPv ? backtestResults.SqSampledLists["twrPV"] : backtestResults.SqSampledLists["rawPV"];
        else // If SamplingQcOriginal, then ChartPoints => p_pv = new List<DateValue>
        {
            List<ChartPoint> equityChart = backtestResults.Charts["Strategy Equity"].Series["Equity"].Values;

            if (equityChart.Count < 2)
                Console.WriteLine($"Warning! The Equity (PV) Chart has only {equityChart.Count} items.");
            else
                Console.WriteLine($"#Charts:{backtestResults.Charts.Count}. The Equity (PV) chart: {equityChart[0].y:N0}, {equityChart[1].y:N0} ... {equityChart[^2].y:N0}, {equityChart[^1].y:N0}");

            // With Minute resolution simulation, the PV chart is generated at every 5 minutes. But the first point of the day is UTC 4:00, then 13:31, 13:36, 13:41,...
            if (equityChart.Count >= 3) // because the first is a dummy point, we need at least 3 data points to decide.
            {
                int diffBetween2points = (int)(equityChart[2].x - equityChart[1].x);
                if (diffBetween2points <= 60)
                    p_chartResolution = ChartResolution.Minute;
                else if (diffBetween2points <= 300)
                    p_chartResolution = ChartResolution.Minute5;
                else
                    p_chartResolution = ChartResolution.Daily;
            }

            if (p_chartResolution == ChartResolution.Daily)
            {
                // Eliminate daily chart duplicates. There is 1 point for weekends, but 2 points (morning, market close) for the weekdays. We keep only the last Y value for the day.
                DateTime currentDate = DateTime.MinValue; // initialize currentDate to the smallest possible value
                for (int i = 0; i < equityChart.Count; i++)
                {
                    ChartPoint item = equityChart[i];
                    // convert the Unix timestamp (item.x) to a DateTime object and take only the date part
                    DateTime itemDate = DateTimeOffset.FromUnixTimeSeconds(item.x).DateTime.Date;
                    if (itemDate != currentDate) // if this is a new date, add a new point to p_pv
                    {
                        p_pv.Add(new DateValue { Date = itemDate, Value = (float)item.y });
                        currentDate = itemDate; // set currentDate to the new date
                    }
                    else // if this is the same date as the previous point, update the existing point
                        p_pv[^1] = new DateValue { Date = itemDate, Value = (float)item.y };
                }
            }
            else // PerMinute Data
            {
                for (int i = 0; i < equityChart.Count; i++)
                {
                    DateTime itemDate = DateTimeOffset.FromUnixTimeSeconds(equityChart[i].x).DateTime;
                    p_pv.Add(new DateValue { Date = itemDate, Value = (float)equityChart[i].y });
                }
            }
        }
        // Step 2: create the p_stat of the result.
        if (p_sqResultStat != SqResultStat.NoStat)
        {
            Dictionary<string, string> finalStat = backtestResults.FinalStatistics;
            var statisticsStr = $"{Environment.NewLine}" + $"{string.Join(Environment.NewLine, finalStat.Select(x => $"STATISTICS:: {x.Key} {x.Value}"))}";
            Console.WriteLine(statisticsStr);

            p_stat.StartPortfolioValue = (float)backtestResults.StartingPortfolioValue;
            p_stat.EndPortfolioValue = (float)backtestResults.DailyPortfolioValue;
            if (!finalStat.IsNullOrEmpty())
            {
                p_stat.TotalReturn = float.Parse(finalStat[PerformanceMetrics.NetProfit].Replace("%", string.Empty));
                p_stat.CAGR = float.Parse(finalStat[PerformanceMetrics.CompoundingAnnualReturn].Replace("%", string.Empty));
                p_stat.StDev = float.Parse(finalStat[PerformanceMetrics.AnnualStandardDeviation]);
                if (float.IsNaN(p_stat.StDev)) // annualized daily StDev. If histDailyPctChgs is empty, StDev becomes NaN, which is correct , but we don't want to send NaN to clients.
                    p_stat.StDev = 0;
                if (p_stat.Sharpe > 100f)
                    p_stat.Sharpe = float.NaN; // if value is obviously wrong, indicate that with NaN
                p_stat.Sharpe = float.Parse(finalStat[PerformanceMetrics.SharpeRatio]);
                if (finalStat.TryGetValue(PerformanceMetrics.CagrSharpeRatio, out string? cagrSharpeStr)) // CagrSharpeRatio is calculated in SqCore, but not in original QC code
                    p_stat.CagrSharpe = float.Parse(cagrSharpeStr);
                p_stat.MaxDD = float.Parse(finalStat[PerformanceMetrics.Drawdown].Replace("%", string.Empty));

                p_stat.NTrades = int.Parse(finalStat[PerformanceMetrics.TotalTrades]);

                // Ulcer - To be added, but these are not cardinal at the moment.
                // p_stat.Sortino = float.Parse(finalStat[PerformanceMetrics.SharpeRatio].Replace("%", string.Empty));
                // if (p_stat.Sortino > 100f)
                //     p_stat.Sortino = float.NaN; // if value is obviously wrong, indicate that with NaN

                // p_stat.WinRate = float.Parse(finalStat[PerformanceMetrics.WinRate].Replace("%", string.Empty));
                // p_stat.LossRate = float.Parse(finalStat[PerformanceMetrics.LossRate].Replace("%", string.Empty));
                // p_stat.Turnover = float.Parse(finalStat["Portfolio Turnover"]);
                // p_stat.LongShortRatio = float.Parse(finalStat["Long/Short Ratio"].Replace("%", string.Empty));
                p_stat.Fees = float.Parse(finalStat[PerformanceMetrics.TotalFees].Replace("$", string.Empty));
                // BenchmarkCAGR - To be added
                // BenchmarkMaxDrawDown - To be added
                // CorrelationWithBenchmark - To be added
            }
        }

        // We need these in the Statistic: "Net Profit" => TotalReturn, "Compounding Annual Return" =>CAGR, "Drawdown" => MaxDD,  "Sharpe Ratio" =>Sharpe, "Win Rate" =>WinRate, "Annual Standard Deviation" =>StDev, "Sortino Ratio" => Sortino, "Portfolio Turnover" => Turnover, "Long/Short Ratio" =>LongShortRatio, "Total Fees" => Fees,

        // Step 3: create the p_prtfPoss of the result.
        var prtfPositions = backtestResults.Algorithm;
        // Get the real-time price from AssetCache only if EndTime.Date is TodayUtc: this can be if EndTime is not specified at all or if it is specified but it is exactly TodayUtc.
        bool getEstPriceAsRealTime = (p_forcedEndTimeUtc == null) || p_forcedEndTimeUtc?.Date == DateTime.UtcNow.Date;
        foreach (Security? security in prtfPositions.UniverseManager.ActiveSecurities.Values)
        {
            if ((int)security.Holdings.Quantity == 0) // eliminating the positions with holding quantity equals to zero
                continue;

            // TEMP: trying to get Company Name from the Fundamental data stream.
            // Equity equity = (Equity)security;  // not necessary
            // var fundamentals = security.Fundamentals;
            // if (fundamentals != null)
            // {
            //     string companyShortName = security.Fundamentals.CompanyReference.ShortName;
            //     // string companyShortName = security.Fundamentals.CompanyReference.ShortName;
            //     Console.WriteLine(companyShortName);
            // }

            PortfolioPosition posStckItem = new()
            {
                SqTicker = "S/" + security.Holdings.Symbol.ToString(),
                Quantity = (float)security.Holdings.Quantity,
                AvgPrice = (float)security.Holdings.AveragePrice,
                BacktestLastPrice = (float)security.Holdings.Price // last price known by the backtest. If p_forcedEndTimeUtc == Null or TodayUtc, then this is the yesterdayClose. Otherwise, it is the Close price on that EndTime.Date day.
            };

            if (getEstPriceAsRealTime)
            {
                Asset? asset = MemDb.gMemDb.AssetsCache.TryGetAsset(posStckItem.SqTicker);
                if (asset != null)
                    posStckItem.EstPrice = MemDb.gMemDb.GetLastRtValue(asset);
                else
                    posStckItem.EstPrice = (float)security.Holdings.Price;
            }
            else
                posStckItem.EstPrice = (float)security.Holdings.Price; // EndTimeUtc is a proper past date, not TodayUtc, so the EstPrice is the price on that given past date.
            p_prtfPoss.Add(posStckItem); // Stock Tickers
        }

        PortfolioPosition posCashItem = new(); // Cash Tickers
        foreach (var item in prtfPositions.Portfolio.CashBook.Values)
        {
            posCashItem.SqTicker = "C/" + item.Symbol.ToString();
            posCashItem.Quantity = (float)item.Amount;
            posCashItem.AvgPrice = 1.0f; // for Cash positions the price is the FX Exchange rate for that currency. So, FX rate yesterday vs. realtime can be put into PrevDayPrice vs. realtime price. For USD, as base currency, the FX exchange rate is always 1.0
            posCashItem.BacktestLastPrice = 1.0f;
            p_prtfPoss.Add(posCashItem);
        }
        return null; // No Error
    }

    // e.g. AlgortihmParam with Dates : "startDate=2002-07-24&endDate=2024-02-08&assets=SPY,TLT&weights=60,40&rebFreq=Daily,30d";
    // e.g. AlgorithmParam without Dates : "assets=SPY,TLT&weights=60,40&rebFreq=Daily,10d"
    public static string GetBacktestAlgorithmParam(DateTime? p_forcedStartDate, DateTime? p_forcedEndDate, string p_algorithmParam)
    {
        // Get the original AlgorithmParam value
        string backtestAlgorithmParam = p_algorithmParam;

        // AlgorithmParam itself 'can' have StartDate, EndDate. But ChartGenerator can further restricts the period with p_forcedStartDate/p_forcedEndDate
        // Update endDate in AlgorithmParam if p_forcedEndDate is not null
        if (p_forcedEndDate != null)
        {
            string forcedEndDateTimeUtcStr = Utils.Date2hYYYYMMDDTHHMMSS(p_forcedEndDate.Value);
            int endDateIndex = backtestAlgorithmParam.IndexOf("endDate=");
            if (endDateIndex == -1)
                backtestAlgorithmParam = "endDate=" + forcedEndDateTimeUtcStr + "&" + backtestAlgorithmParam; // "endDate=" not found, add to the front
            else
            {
                // "endDate=" found, replace the value
                int endIndex = backtestAlgorithmParam.IndexOf('&', endDateIndex);
                if (endIndex == -1)
                    endIndex = backtestAlgorithmParam.Length;

                // Replace the value associated with "endDate=" with the new value p_forcedEndDate
                backtestAlgorithmParam = backtestAlgorithmParam[..(endDateIndex + "endDate=".Length)] + forcedEndDateTimeUtcStr + backtestAlgorithmParam[endIndex..];
            }
        }

        // Update startDate in AlgorithmParam if p_forcedStartDate is not null
        if (p_forcedStartDate != null)
        {
            string forcedStartDateTimeUtcStr = Utils.Date2hYYYYMMDDTHHMMSS(p_forcedStartDate.Value);
            int startDateIndex = backtestAlgorithmParam.IndexOf("startDate=");
            if (startDateIndex == -1)
                backtestAlgorithmParam = "startDate=" + forcedStartDateTimeUtcStr + "&" + backtestAlgorithmParam; // "startDate=" not found, add to the front
            else
            {
                // "startDate=" found, replace the value
                int endIndex = backtestAlgorithmParam.IndexOf('&', startDateIndex);
                if (endIndex == -1)
                    endIndex = backtestAlgorithmParam.Length;

                // Replace the value associated with "startDate=" with the new value p_forcedStartDate
                backtestAlgorithmParam = backtestAlgorithmParam[..(startDateIndex + "startDate=".Length)] + forcedStartDateTimeUtcStr + backtestAlgorithmParam[endIndex..];
            }
        }

        return backtestAlgorithmParam; // Return the updated AlgorithmParam
    }

    public static string? GetBmrksHistoricalResults(string p_bmrksStr, DateTime p_minDate, out PriceHistoryJs p_histPrices, out ChartResolution p_chartResolution)
    {
        PriceHistoryJs historicalPrices = new();
        p_chartResolution = ChartResolution.Daily;
        string tickerAsTradedToday = p_bmrksStr; // if symbol.zip doesn't exist in Data folder, it will not download it (cost money, you have to download in their shop). It raises an exception.
        Symbol symbol = new(SecurityIdentifier.GenerateEquity(tickerAsTradedToday, Market.USA, true, FinDb.gFinDb.MapFileProvider), tickerAsTradedToday);

        DateTime startTimeUtc = new(p_minDate.Year, p_minDate.Month, p_minDate.Day);
        // DateTime startTimeUtc = new(2008, 01, 01);
        // If you want to get 20080104 day data too, it has to be specified like this:
        // class TimeBasedFilter assures that (data.EndTime <= EndTimeLocal)
        // It is assumed that any TradeBar final values are only released at TradeBar.EndTime (OK for minute, hourly data, but not perfect for daily data which is known at 16:00)
        // Any TradeBar's EndTime is Time+1day (assuming that ClosePrice is released not at 16:00, but later, at midnight)
        // So the 20080104's row in CVS is: Time: 20080104:00:00, EndTime:20080105:00:00
        DateTime today = DateTime.UtcNow.Date;
        DateTime endTimeUtc = new(today.Year, today.Month, today.Day, 5, 0, 0); // this will be => 2008-01-05:00:00 endTimeLocal

        // Use TickType.TradeBar. That is in the daily CSV file. TickType.Quote file would contains Ask(Open/High/Low/Close) + Bid(Open/High/Low/Close), like a Quote from a Broker at trading realtime.
        var historyRequests = new[]
        {
            new HistoryRequest(startTimeUtc, endTimeUtc, typeof(TradeBar), symbol, Resolution.Daily, SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                TimeZones.NewYork, null, false, false, DataNormalizationMode.Adjusted, QuantConnect.TickType.Trade)
        };

        NodaTime.DateTimeZone sliceTimeZone = TimeZones.NewYork; // "algorithm.TimeZone"

        List<Slice>? result = FinDb.gFinDb.HistoryProvider.GetHistory(historyRequests, sliceTimeZone).ToList(); // see comment in FinDb.HistoryProvider
        Utils.Logger.Info("length of result bar values:" + result[0].Bars.Values.ToArray().Length);
        Console.WriteLine($" Test Historical price data. Number of TradeBars: {result.Count}. SPY RAW ClosePrice on {result[0].Bars.Values.ToArray()[0].EndTime}: {result[0].Bars.Values.ToArray()[0].Close}");
        // find the ChartResolution
        TradeBar[]? resBarVals1 = result[1].Bars.Values.ToArray();
        TradeBar[]? resBarVals2 = result[2].Bars.Values.ToArray();
        // With Minute resolution simulation, the PV chart is generated at every 5 minutes.
        if (result.Count >= 3) // because the first is a dummy point, we need at least 3 data points to decide.
        {
            int diffBetween2points = (int)(resBarVals2[0].EndTime - resBarVals1[0].EndTime).TotalSeconds;
            if (diffBetween2points <= 60)
                p_chartResolution = ChartResolution.Minute;
            else if (diffBetween2points <= 300)
                p_chartResolution = ChartResolution.Minute5;
            else
                p_chartResolution = ChartResolution.Daily;
        }
        for (int i = 0; i < result.Count; i++)
        {
            TradeBar[]? resBarVals = result[i].Bars.Values.ToArray();
            uint dateInt = (uint)new DateTimeOffset(resBarVals[0].EndTime, TimeSpan.Zero).ToUnixTimeSeconds();
            float price = (float)resBarVals[0].Price;

            historicalPrices.Dates.Add(dateInt); // Add the date to the Date list
            historicalPrices.Prices.Add(price); // Add the price to the Price list
        }
        p_histPrices = historicalPrices;

        return null; // No Error
    }
}