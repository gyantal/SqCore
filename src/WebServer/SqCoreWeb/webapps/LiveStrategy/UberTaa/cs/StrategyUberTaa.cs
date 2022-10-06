using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SqCommon;
using FinTechCommon;
using System.Globalization;
using System.Diagnostics;
using MathCommon.MathNet;

namespace SqCoreWeb.Controllers;

[ApiController]
[Route("[controller]")]
[ResponseCache(CacheProfileName = "NoCache")]
public class StrategyUberTaaController : ControllerBase
{
    enum Universe : byte { GameChangers = 1, GlobalAssets = 2 };

    public class DailyData
    {
        public DateTime Date { get; set; }
        public double AdjClosePrice { get; set; }
    }

    [HttpGet]
    public ActionResult Index(int universe, int winnerRun)
    {
        return universe switch
        {
            // 1: GameChanger, 2: Global Assets
            1 or 2 => Content(GetResultStr((Universe)universe, winnerRun == 1), "text/html"),
            _ => Content("Error", "text/html"),
        };
    }

    string GetResultStr(Universe p_universe, bool p_winnerRun)
    {
        int thresholdLower = 25; // Upper threshold is 100-thresholdLower.
        int[] lookbackDays = new int[] { 60, 120, 180, 252 };
        int volDays = 20;

        // string[] gchAssetList = new string[]{ "AAPL", "ADBE", "AMZN", "CRM", "CRWD", "ETSY", "META", "GOOGL", "MA", "MSFT", "NOW", "NVDA", "PYPL", "QCOM", "SE", "SHOP", "SQ", "V", "TLT"}; //TLT is used as a cashEquivalent
        // string[] gmrAssetList = new string[] { "MDY", "ILF", "FEZ", "EEM", "EPP", "VNQ", "TLT" }; //TLT is used as a cashEquivalent
        string titleString = string.Empty, warningGCh = string.Empty, usedGSheetUrl = string.Empty, usedGDocUrl = string.Empty;
        string? usedGSheetStr = null;
        string[] usedAssetList = Array.Empty<string>();
        switch (p_universe)
        {
            case Universe.GameChangers:
                titleString = "GameChangers";
                usedGDocUrl = "https://docs.google.com/document/d/1JPyRJY7VrW7hQMagYLtB_ruTzEKEd8POHQy6sZ_Nnyk";
                usedGSheetUrl = "https://docs.google.com/spreadsheets/d/1AGci_xFhgcC-Q1tEZ5E-HTBWbOU-C9ZXyjLIN1bEZeE";
                usedGSheetStr = UberTaaGoogleApiGsheet("https://sheets.googleapis.com/v4/spreadsheets/1AGci_xFhgcC-Q1tEZ5E-HTBWbOU-C9ZXyjLIN1bEZeE/values/A1:AF2000?key=");
                usedAssetList = GetTickersFromGSheet(usedGSheetStr) ?? Array.Empty<string>();
                warningGCh = "WARNING! Trading rules have been changed! Only live positions are valid, required trades are not!";
                break;
            case Universe.GlobalAssets:
                titleString = "Global Assets";
                usedGDocUrl = "https://docs.google.com/document/d/1-hDoFu1buI1XHvJZyt6Cq813Hw1TQWGl0jE7mwwS3l0";
                usedGSheetUrl = "https://docs.google.com/spreadsheets/d/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU";
                usedGSheetStr = UberTaaGoogleApiGsheet("https://sheets.googleapis.com/v4/spreadsheets/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU/values/A1:Z2000?key=");
                usedAssetList = GetTickersFromGSheet(usedGSheetStr) ?? Array.Empty<string>();
                break;
        }

        string[] clmtAssetList = new string[] { "SPY", "XLU", "VTI" };    // CMLT: Combined Leverage Market Timer
        string[] allAssetList = new string[clmtAssetList.Length + usedAssetList.Length]; // Joining 2 arrays[]: LINQ has Concat() for the enumerable, but this Array Copy is the fastest implementation
        clmtAssetList.CopyTo(allAssetList, 0);
        usedAssetList.CopyTo(allAssetList, clmtAssetList.Length);

        // Collecting and splitting price data got from SQL Server
        (IList<List<DailyData>>, List<List<DailyData>>, List<DailyData>) dataListTupleFromSQServer = GetStockHistData(allAssetList);

        IList<List<DailyData>> quotesData = dataListTupleFromSQServer.Item1;
        IList<List<DailyData>> quotesForClmtData = dataListTupleFromSQServer.Item2;
        List<DailyData> cashEquivalentQuotesData = dataListTupleFromSQServer.Item3;

        Debug.WriteLine("The Data from gSheet is :", quotesData, quotesForClmtData, cashEquivalentQuotesData);

        // Calculating basic weights based on percentile channels - base Varadi TAA
        Tuple<double[], double[,]> taaWeightResultsTuple = TaaWeights(quotesData, lookbackDays, volDays, thresholdLower, p_winnerRun);
        Debug.WriteLine("The Data from gSheet is :", taaWeightResultsTuple);
        // // Calculating CLMT data
        double[][] clmtRes = CLMTCalc(quotesForClmtData);

        // Setting last data date
        double lastDataDate = (clmtRes[0][^1] == taaWeightResultsTuple.Item1[^1]) ? clmtRes[0][^1] : 0;

        // Get, split and convert GSheet data
        // string? gSheetString = UberTaaGoogleApiGsheet(usedGSheetRef);
        Tuple<double[], int[,], int[], int[], string[], int[], int[]> gSheetResToFinCalc = GSheetConverter(usedGSheetStr, allAssetList);
        Debug.WriteLine("The Data from gSheet is :", gSheetResToFinCalc);

        // Calculating final weights - Advanced UberTAA
        Tuple<double[,], double[,], double[,], string[], string[]> weightsFinal = MultiplFinCalc(clmtRes, gSheetResToFinCalc, allAssetList, lastDataDate, taaWeightResultsTuple);

        // Request time (UTC)
        DateTime liveDateTime = DateTime.UtcNow;
        string liveDate = liveDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        DateTime timeNowET = Utils.ConvertTimeFromUtcToEt(liveDateTime);
        string liveDateString = "Request time (UTC): " + liveDate;

        // Last data time (UTC)
        string lastDataTime = (quotesData[0][^1].Date.Date == liveDateTime.Date & timeNowET.TimeOfDay <= new DateTime(2000, 1, 1, 16, 15, 0).TimeOfDay) ? "Live data at " + liveDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "Close price on " + quotesData[0][^1].Date.ToString("yyyy-MM-dd");
        string lastDataTimeString = "Last data time (UTC): " + lastDataTime;

        // Current PV, Number of current and required shares
        DateTime startMatlabDate = DateTime.ParseExact("1900/01/01", "yyyy/MM/dd", CultureInfo.InvariantCulture);
        DateTime nextTradingDay = startMatlabDate.AddDays(weightsFinal.Item1[weightsFinal.Item1.GetLength(0) - 1, 0] - 693962);
        string nextTradingDayString = nextTradingDay.ToString("yyyy-MM-dd");
        DateTime currPosDate = startMatlabDate.AddDays(gSheetResToFinCalc.Item6[0] - 693962);
        string currPosDateString = currPosDate.ToString("yyyy-MM-dd");

        double currPV;
        int[] currPosInt = new int[usedAssetList.Length + 1];

        double[] currPosValue = new double[usedAssetList.Length + 1];
        for (int jCols = 0; jCols < currPosValue.Length - 2; jCols++)
        {
            currPosInt[jCols] = gSheetResToFinCalc.Item7[jCols];
            currPosValue[jCols] = quotesData[jCols][quotesData[0].Count - 1].AdjClosePrice * currPosInt[jCols];
        }
        currPosInt[^2] = gSheetResToFinCalc.Item7[^1];
        currPosInt[^1] = gSheetResToFinCalc.Item6[1];
        currPosValue[^2] = cashEquivalentQuotesData[quotesData[0].Count - 1].AdjClosePrice * gSheetResToFinCalc.Item7[^1];
        currPosValue[^1] = gSheetResToFinCalc.Item6[1];
        currPV = Math.Round(currPosValue.Sum());

        double[] nextPosValue = new double[usedAssetList.Length + 1];
        for (int jCols = 0; jCols < nextPosValue.Length - 2; jCols++)
        {
            nextPosValue[jCols] = currPV * weightsFinal.Item3[weightsFinal.Item3.GetLength(0) - 1, jCols + 1];
        }
        nextPosValue[^2] = Math.Max(0, currPV - nextPosValue.Take(nextPosValue.Length - 2).ToArray().Sum());
        nextPosValue[^1] = currPV - nextPosValue.Take(nextPosValue.Length - 1).ToArray().Sum();

        double[] nextPosInt = new double[nextPosValue.Length];
        for (int jCols = 0; jCols < nextPosInt.Length - 2; jCols++)
        {
            nextPosInt[jCols] = nextPosValue[jCols] / quotesData[jCols][quotesData[0].Count - 1].AdjClosePrice;
        }
        nextPosInt[^2] = nextPosValue[nextPosInt.Length - 2] / cashEquivalentQuotesData[quotesData[0].Count - 1].AdjClosePrice;
        nextPosInt[^1] = nextPosValue[nextPosInt.Length - 1];

        double[] posValueDiff = new double[usedAssetList.Length + 1];
        for (int jCols = 0; jCols < posValueDiff.Length; jCols++)
        {
            posValueDiff[jCols] = nextPosValue[jCols] - currPosValue[jCols];
        }

        double[] posIntDiff = new double[usedAssetList.Length + 1];
        for (int jCols = 0; jCols < posIntDiff.Length; jCols++)
        {
            posIntDiff[jCols] = nextPosInt[jCols] - currPosInt[jCols];
        }

        // CLMT: Combined Leverage Market Timer
        string clmtSignal;
        if (clmtRes[7][^1] == 1)
        {
            clmtSignal = "bullish";
        }
        else if (clmtRes[7][^1] == 3)
        {
            clmtSignal = "bearish";
        }
        else
        {
            clmtSignal = "neutral";
        }

        string xluVtiSignal;
        if (clmtRes[3][^1] == 1)
        {
            xluVtiSignal = "bullish";
        }
        else
        {
            xluVtiSignal = "bearish";
        }

        string spxMASignal;
        if (clmtRes[6][^1] == 1)
        {
            spxMASignal = "bullish";
        }
        else
        {
            spxMASignal = "bearish";
        }


        // Position weights in the last 20 days
        string[,] prevPosMtx = new string[weightsFinal.Item3.GetLength(0) + 1, usedAssetList.Length + 3];
        for (int iRows = 0; iRows < prevPosMtx.GetLength(0) - 1; iRows++)
        {
            DateTime assDate = startMatlabDate.AddDays(weightsFinal.Item3[iRows, 0] - 693962);
            string assDateString = assDate.ToString("yyyy-MM-dd");
            prevPosMtx[iRows, 0] = assDateString;

            double assetWeightSum = 0;
            for (int jCols = 0; jCols < prevPosMtx.GetLength(1) - 4; jCols++)
            {
                assetWeightSum += weightsFinal.Item3[iRows, jCols + 1];
                prevPosMtx[iRows, jCols + 1] = Math.Round(weightsFinal.Item3[iRows, jCols + 1] * 100.0, 2).ToString() + "%";
            }
            prevPosMtx[iRows, prevPosMtx.GetLength(1) - 1] = (weightsFinal.Item4[iRows] == "0") ? "---" : weightsFinal.Item4[iRows];
            prevPosMtx[iRows, prevPosMtx.GetLength(1) - 3] = Math.Round(Math.Max((1.0 - assetWeightSum), 0) * 100.0, 2).ToString() + "%";
            prevPosMtx[iRows, prevPosMtx.GetLength(1) - 2] = Math.Round((1.0 - assetWeightSum - Math.Max((1.0 - assetWeightSum), 0)) * 100.0, 2).ToString() + "%";
        }
        prevPosMtx[prevPosMtx.GetLength(0) - 1, 0] = string.Empty;
        for (int jCols = 0; jCols < prevPosMtx.GetLength(1) - 3; jCols++)
        {
            prevPosMtx[prevPosMtx.GetLength(0) - 1, jCols + 1] = usedAssetList[jCols];
        }
        prevPosMtx[prevPosMtx.GetLength(0) - 1, prevPosMtx.GetLength(1) - 2] = "Cash";
        prevPosMtx[prevPosMtx.GetLength(0) - 1, prevPosMtx.GetLength(1) - 1] = "Event";

        for (int iRows = 0; iRows < prevPosMtx.GetLength(0) / 2; iRows++)
        {
            for (int jCols = 0; jCols < prevPosMtx.GetLength(1); jCols++)
            {
                string tmp = prevPosMtx[iRows, jCols];
                prevPosMtx[iRows, jCols] = prevPosMtx[prevPosMtx.GetLength(0) - iRows - 1, jCols];
                prevPosMtx[prevPosMtx.GetLength(0) - iRows - 1, jCols] = tmp;
            }
        }

        // Codes for last 20 days to coloring
        double[,] prevAssEventCodes = weightsFinal.Item1;
        for (int iRows = 0; iRows < prevAssEventCodes.GetLength(0) / 2; iRows++)
        {
            for (int jCols = 0; jCols < prevAssEventCodes.GetLength(1); jCols++)
            {
                (prevAssEventCodes[prevAssEventCodes.GetLength(0) - iRows - 1, jCols], prevAssEventCodes[iRows, jCols]) = (prevAssEventCodes[iRows, jCols], prevAssEventCodes[prevAssEventCodes.GetLength(0) - iRows - 1, jCols]);
            }
        }

        // Color codes for last 20 days
        string[,] prevAssEventColorMtx = new string[weightsFinal.Item3.GetLength(0) + 1, usedAssetList.Length + 3];
        for (int iRows = 0; iRows < prevAssEventColorMtx.GetLength(0) - 1; iRows++)
        {
            prevAssEventColorMtx[0, 0] = "66CCFF";
            prevAssEventColorMtx[0, prevAssEventColorMtx.GetLength(1) - 3] = "66CCFF";
            prevAssEventColorMtx[0, prevAssEventColorMtx.GetLength(1) - 2] = "66CCFF";
            prevAssEventColorMtx[0, prevAssEventColorMtx.GetLength(1) - 1] = "66CCFF";
            prevAssEventColorMtx[iRows + 1, 0] = "FF6633";
            prevAssEventColorMtx[iRows + 1, prevAssEventColorMtx.GetLength(1) - 3] = "FFE4C4";
            prevAssEventColorMtx[iRows + 1, prevAssEventColorMtx.GetLength(1) - 2] = "FFE4C4";
            prevAssEventColorMtx[iRows + 1, prevAssEventColorMtx.GetLength(1) - 1] = "FFFF00";
            for (int jCols = 0; jCols < prevAssEventColorMtx.GetLength(1) - 4; jCols++)
            {
                prevAssEventColorMtx[0, jCols + 1] = "66CCFF";
                if (prevAssEventCodes[iRows, jCols + 1] == 1)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "228B22";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 2)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "FF0000";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 3)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "7CFC00";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 4)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "DC143C";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 5)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "1E90FF";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 6)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "7B68EE";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 7)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "FFFFFF";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 8)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "00FFFF";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 9)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "A9A9A9";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 10)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "FF8C00";
                }
                else if (prevAssEventCodes[iRows, jCols + 1] == 11)
                {
                    prevAssEventColorMtx[iRows + 1, jCols + 1] = "F0E68C";
                }
            }
        }


        // Events in the next 10 days
        string[,] futPosMtx = new string[weightsFinal.Item2.GetLength(0) + 1, usedAssetList.Length + 1];
        string[,] futAssEventCodes = new string[weightsFinal.Item2.GetLength(0) + 1, usedAssetList.Length + 1];
        for (int iRows = 0; iRows < futPosMtx.GetLength(0) - 1; iRows++)
        {
            DateTime assFDate = startMatlabDate.AddDays(weightsFinal.Item2[iRows, 0] - 693962);
            string assFDateString = assFDate.ToString("yyyy-MM-dd");
            futPosMtx[iRows + 1, 0] = assFDateString;
            futAssEventCodes[iRows + 1, 0] = "FF6633";

            for (int jCols = 0; jCols < futPosMtx.GetLength(1) - 2; jCols++)
            {
                if (weightsFinal.Item2[iRows, jCols + 1] == 1)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "FOMC Bullish Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "228B22";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 2)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "FOMC Bearish Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "FF0000";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 3)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "Holiday Bullish Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "7CFC00";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 4)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "Holiday Bearish Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "DC143C";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 5)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "Important Earnings Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "1E90FF";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 6)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "Pre-Earnings Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "7B68EE";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 7)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "Skipped Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "FFFFFF";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 8)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "CLMT Bullish Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "00FFFF";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 9)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "CLMT Neutral Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "A9A9A9";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 10)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "CLMT Bearish Day";
                    futAssEventCodes[iRows + 1, jCols + 1] = "FF8C00";
                }
                else if (weightsFinal.Item2[iRows, jCols + 1] == 11)
                {
                    futPosMtx[iRows + 1, jCols + 1] = "---"; // Unknown CLMT Day
                    futAssEventCodes[iRows + 1, jCols + 1] = "F0E68C";
                }
            }

            futPosMtx[iRows + 1, futPosMtx.GetLength(1) - 1] = (weightsFinal.Item5[iRows] == "0") ? "---" : weightsFinal.Item5[iRows];
            futAssEventCodes[iRows + 1, futPosMtx.GetLength(1) - 1] = "FFFF00";
        }
        futPosMtx[0, 0] = string.Empty;
        futAssEventCodes[0, 0] = "66CCFF";
        for (int jCols = 0; jCols < futPosMtx.GetLength(1) - 2; jCols++)
        {
            futPosMtx[0, jCols + 1] = usedAssetList[jCols];
            futAssEventCodes[0, jCols + 1] = "66CCFF";
        }

        futPosMtx[0, futPosMtx.GetLength(1) - 1] = "Event";
        futAssEventCodes[0, futPosMtx.GetLength(1) - 1] = "66CCFF";


        // AssetPrice Changes in last 20 days to chart
        int assetChartLength = 20;
        string[,] assetChangesMtx = new string[assetChartLength + 1, usedAssetList.Length];
        for (int iRows = 0; iRows < assetChangesMtx.GetLength(0); iRows++)
        {
            assetChangesMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            for (int jCols = 0; jCols < assetChangesMtx.GetLength(1) - 1; jCols++)
            {
                assetChangesMtx[iRows, jCols + 1] = Math.Round((quotesData[jCols][quotesData[jCols].Count - 1 - assetChartLength + iRows].AdjClosePrice / quotesData[jCols][quotesData[jCols].Count - 1 - assetChartLength].AdjClosePrice - 1) * 100.0, 2).ToString() + "%";
            }
        }

        // Daily changes, currently does not used.
        string[,] assetDailyChangesMtx = new string[assetChartLength + 1, usedAssetList.Length];
        for (int iRows = 0; iRows < assetDailyChangesMtx.GetLength(0); iRows++)
        {
            assetDailyChangesMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            for (int jCols = 0; jCols < assetDailyChangesMtx.GetLength(1) - 1; jCols++)
            {
                assetDailyChangesMtx[iRows, jCols + 1] = Math.Round((quotesData[jCols][quotesData[jCols].Count - 1 - assetChartLength + iRows].AdjClosePrice / quotesData[jCols][quotesData[jCols].Count - 1 - assetChartLength + iRows - 1].AdjClosePrice - 1) * 100.0, 2).ToString() + "%";
            }
        }

        // Data for SPX MA chart
        string[,] spxToChartMtx = new string[assetChartLength + 1, 4];
        for (int iRows = 0; iRows < spxToChartMtx.GetLength(0); iRows++)
        {
            spxToChartMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            spxToChartMtx[iRows, 1] = Math.Round(clmtRes[8][clmtRes[8].GetLength(0) - assetChartLength - 1 + iRows], 0).ToString();
            spxToChartMtx[iRows, 2] = Math.Round(clmtRes[4][clmtRes[4].GetLength(0) - assetChartLength - 1 + iRows], 0).ToString();
            spxToChartMtx[iRows, 3] = Math.Round(clmtRes[5][clmtRes[5].GetLength(0) - assetChartLength - 1 + iRows], 0).ToString();
        }

        // Data for XLU-VTi RSI chart
        string[,] xluVtiToChartMtx = new string[assetChartLength + 1, 3];
        for (int iRows = 0; iRows < spxToChartMtx.GetLength(0); iRows++)
        {
            xluVtiToChartMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            xluVtiToChartMtx[iRows, 1] = Math.Round(clmtRes[1][clmtRes[1].GetLength(0) - assetChartLength - 1 + iRows], 0).ToString();
            xluVtiToChartMtx[iRows, 2] = Math.Round(clmtRes[2][clmtRes[2].GetLength(0) - assetChartLength - 1 + iRows], 0).ToString();
        }


        // Creating input string for JavaScript.
        StringBuilder sb = new("{" + Environment.NewLine);
        sb.Append(@"""titleCont"": """ + titleString);
        sb.Append(@"""," + Environment.NewLine + @"""warningCont"": """ + warningGCh);
        sb.Append(@"""," + Environment.NewLine + @"""requestTime"": """ + liveDateString);
        sb.Append(@"""," + Environment.NewLine + @"""lastDataTime"": """ + lastDataTimeString);
        sb.Append(@"""," + Environment.NewLine + @"""currentPV"": """ + currPV.ToString("#,##0"));
        sb.Append(@"""," + Environment.NewLine + @"""currentPVDate"": """ + currPosDateString);
        sb.Append(@"""," + Environment.NewLine + @"""clmtSign"": """ + clmtSignal);
        sb.Append(@"""," + Environment.NewLine + @"""xluVtiSign"": """ + xluVtiSignal);
        sb.Append(@"""," + Environment.NewLine + @"""spxMASign"": """ + spxMASignal);
        sb.Append(@"""," + Environment.NewLine + @"""gDocRef"": """ + usedGDocUrl);
        sb.Append(@"""," + Environment.NewLine + @"""gSheetRef"": """ + usedGSheetUrl);

        sb.Append(@"""," + Environment.NewLine + @"""assetNames"": """);
        for (int i = 0; i < usedAssetList.Length - 1; i++)
            sb.Append(usedAssetList[i] + ", ");
        sb.Append(usedAssetList[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""assetNames2"": """);
        for (int i = 0; i < usedAssetList.Length; i++)
            sb.Append(usedAssetList[i] + ", ");
        sb.Append("Cash");

        sb.Append(@"""," + Environment.NewLine + @"""currPosNum"": """);
        for (int i = 0; i < currPosInt.Length - 1; i++)
            sb.Append(currPosInt[i].ToString() + ", ");
        sb.Append("$" + Math.Round(currPosInt[^1] / 1000.0).ToString() + "K");

        sb.Append(@"""," + Environment.NewLine + @"""currPosVal"": """);
        for (int i = 0; i < currPosValue.Length - 1; i++)
            sb.Append("$" + Math.Round(currPosValue[i] / 1000).ToString() + "K, ");
        sb.Append("$" + Math.Round(currPosValue[^1] / 1000).ToString() + "K");

        sb.Append(@"""," + Environment.NewLine + @"""nextPosNum"": """);
        for (int i = 0; i < nextPosInt.Length - 1; i++)
            sb.Append(Math.Round(nextPosInt[i]).ToString() + ", ");
        sb.Append("$" + Math.Round(nextPosInt[^1] / 1000).ToString() + "K");

        sb.Append(@"""," + Environment.NewLine + @"""nextPosVal"": """);
        for (int i = 0; i < nextPosValue.Length - 1; i++)
            sb.Append("$" + Math.Round(nextPosValue[i] / 1000).ToString() + "K, ");
        sb.Append("$" + Math.Round(nextPosValue[^1] / 1000).ToString() + "K");

        sb.Append(@"""," + Environment.NewLine + @"""posNumDiff"": """);
        for (int i = 0; i < posIntDiff.Length - 1; i++)
            sb.Append(Math.Round(posIntDiff[i]).ToString() + ", ");
        sb.Append("$" + Math.Round(posIntDiff[^1] / 1000).ToString() + "K");

        sb.Append(@"""," + Environment.NewLine + @"""posValDiff"": """);
        for (int i = 0; i < posValueDiff.Length - 1; i++)
            sb.Append("$" + Math.Round(posValueDiff[i] / 1000).ToString() + "K, ");
        sb.Append("$" + Math.Round(posValueDiff[^1] / 1000).ToString() + "K");

        sb.Append(@"""," + Environment.NewLine + @"""nextTradingDay"": """ + nextTradingDayString);
        sb.Append(@"""," + Environment.NewLine + @"""currPosDate"": """ + currPosDateString);

        sb.Append(@"""," + Environment.NewLine + @"""prevPositionsMtx"": """);
        for (int i = 0; i < prevPosMtx.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < prevPosMtx.GetLength(1) - 1; j++)
            {
                sb.Append(prevPosMtx[i, j] + ", ");
            }
            sb.Append(prevPosMtx[i, prevPosMtx.GetLength(1) - 1]);
            if (i < prevPosMtx.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""prevAssEventMtx"": """);
        for (int i = 0; i < prevAssEventColorMtx.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < prevAssEventColorMtx.GetLength(1) - 1; j++)
            {
                sb.Append(prevAssEventColorMtx[i, j] + ",");
            }
            sb.Append(prevAssEventColorMtx[i, prevAssEventColorMtx.GetLength(1) - 1]);
            if (i < prevAssEventColorMtx.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }


        sb.Append(@"""," + Environment.NewLine + @"""futPositionsMtx"": """);
        for (int i = 0; i < futPosMtx.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < futPosMtx.GetLength(1) - 1; j++)
            {
                sb.Append(futPosMtx[i, j] + ", ");
            }
            sb.Append(futPosMtx[i, futPosMtx.GetLength(1) - 1]);
            if (i < futPosMtx.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""futAssEventMtx"": """);
        for (int i = 0; i < futAssEventCodes.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < futAssEventCodes.GetLength(1) - 1; j++)
            {
                sb.Append(futAssEventCodes[i, j] + ",");
            }
            sb.Append(futAssEventCodes[i, futAssEventCodes.GetLength(1) - 1]);
            if (i < futAssEventCodes.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""chartLength"": """ + assetChartLength);

        sb.Append(@"""," + Environment.NewLine + @"""assetChangesToChartMtx"": """);
        for (int i = 0; i < assetChangesMtx.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < assetChangesMtx.GetLength(1) - 1; j++)
            {
                sb.Append(assetChangesMtx[i, j] + ", ");
            }
            sb.Append(assetChangesMtx[i, assetChangesMtx.GetLength(1) - 1]);
            if (i < assetChangesMtx.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""assetDailyChangesToChartMtx"": """);
        for (int i = 0; i < assetDailyChangesMtx.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < assetDailyChangesMtx.GetLength(1) - 1; j++)
            {
                sb.Append(assetDailyChangesMtx[i, j] + ", ");
            }
            sb.Append(assetDailyChangesMtx[i, assetDailyChangesMtx.GetLength(1) - 1]);
            if (i < assetDailyChangesMtx.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""spxMAToChartMtx"": """);
        for (int i = 0; i < spxToChartMtx.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < spxToChartMtx.GetLength(1) - 1; j++)
            {
                sb.Append(spxToChartMtx[i, j] + ", ");
            }
            sb.Append(spxToChartMtx[i, spxToChartMtx.GetLength(1) - 1]);
            if (i < spxToChartMtx.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""xluVtiPercToChartMtx"": """);
        for (int i = 0; i < xluVtiToChartMtx.GetLength(0); i++)
        {
            sb.Append(string.Empty);
            for (int j = 0; j < xluVtiToChartMtx.GetLength(1) - 1; j++)
            {
                sb.Append(xluVtiToChartMtx[i, j] + ", ");
            }
            sb.Append(xluVtiToChartMtx[i, xluVtiToChartMtx.GetLength(1) - 1]);
            if (i < xluVtiToChartMtx.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }


        sb.AppendLine(@"""" + Environment.NewLine + @"}");

        return sb.ToString();
    }

    public string? UberTaaGoogleApiGsheet(string p_usedGSheetRef)
    {
        if (String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) || String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
            return null;

        string? valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync(p_usedGSheetRef + Utils.Configuration["Google:GoogleApiKeyKey"]).TurnAsyncToSyncTask();
        if (valuesFromGSheetStr == null)
            return null;

        return valuesFromGSheetStr;
    }

    public static Tuple< double[], int[,], int[], int[], string[], int[], int[]> GSheetConverter(string? p_gSheetString, string[] p_allAssetList)
    {
        if (p_gSheetString != null)
        {
            string[] gSheetTableRows = p_gSheetString.Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);
            string currPosRaw = gSheetTableRows[3];
            currPosRaw = currPosRaw.Replace("\n", string.Empty).Replace("]", string.Empty).Replace("\",", "BRB").Replace("\"", string.Empty).Replace(" ", string.Empty).Replace(",", string.Empty);
            string[] currPos = currPosRaw.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);
            string[] currPosAP = new string[p_allAssetList.Length - 3];
            Array.Copy(currPos, 2, currPosAP, 0, p_allAssetList.Length - 3);
            int currPosDate = Int32.Parse(currPos[0]);
            int currPosCash = Int32.Parse(currPos[^3]);
            int[] currPosDateCash = new int[] { currPosDate, currPosCash };
            int[] currPosAssets = Array.ConvertAll(currPosAP, int.Parse);

            p_gSheetString = p_gSheetString.Replace("\n", string.Empty).Replace("]", string.Empty).Replace("\"", string.Empty).Replace(" ", string.Empty).Replace(",,", ",0,");
            gSheetTableRows = p_gSheetString.Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);

            string[,] gSheetCodes = new string[gSheetTableRows.Length - 4, currPos.Length];
            string[] gSheetCodesH = new string[currPos.Length];
            for (int iRows = 0; iRows < gSheetCodes.GetLength(0); iRows++)
            {
                gSheetCodesH = gSheetTableRows[iRows + 4].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                for (int jCols = 0; jCols < gSheetCodes.GetLength(1); jCols++)
                {
                    gSheetCodes[iRows, jCols] = gSheetCodesH[jCols];
                }
            }

            gSheetCodes[gSheetCodes.GetLength(0) - 1, gSheetCodes.GetLength(1) - 1] = gSheetCodesH[^1][..gSheetCodesH[^1].IndexOf('}')];

            double[] gSheetDateVec = new double[gSheetCodes.GetLength(0)];
            for (int iRows = 0; iRows < gSheetDateVec.Length; iRows++)
            {
                gSheetDateVec[iRows] = Double.Parse(gSheetCodes[iRows, 0]);
            }

            int[,] gSheetCodesAssets = new int[gSheetCodes.GetLength(0), p_allAssetList.Length - 3];
            for (int iRows = 0; iRows < gSheetCodesAssets.GetLength(0); iRows++)
            {
                for (int jCols = 0; jCols < gSheetCodesAssets.GetLength(1); jCols++)
                {
                    gSheetCodesAssets[iRows, jCols] = Int32.Parse(gSheetCodes[iRows, jCols + 2]);
                }
            }

            int[] gSheetEventCodes = new int[gSheetCodes.GetLength(0)];
            for (int iRows = 0; iRows < gSheetEventCodes.Length; iRows++)
            {
                gSheetEventCodes[iRows] = Int32.Parse(gSheetCodes[iRows, gSheetCodes.GetLength(1) - 3]);
            }

            int[] gSheetEventMultipl = new int[gSheetCodes.GetLength(0)];
            for (int iRows = 0; iRows < gSheetEventMultipl.Length; iRows++)
            {
                gSheetEventMultipl[iRows] = Int32.Parse(gSheetCodes[iRows, gSheetCodes.GetLength(1) - 1]);
            }

            string[] gSheetEventNames = new string[gSheetCodes.GetLength(0)];
            for (int iRows = 0; iRows < gSheetEventNames.Length; iRows++)
            {
                gSheetEventNames[iRows] = gSheetCodes[iRows, gSheetCodes.GetLength(1) - 2];
            }
            Tuple< double[], int[,], int[], int[], string[], int[], int[]> gSheetResFinal = Tuple.Create(gSheetDateVec, gSheetCodesAssets, gSheetEventCodes, gSheetEventMultipl, gSheetEventNames, currPosDateCash, currPosAssets);

            return gSheetResFinal;
        }
        throw new NotImplementedException();
    }
    public static (IList<List<DailyData>>, List<List<DailyData>>, List<DailyData>) GetStockHistData(string[] p_allAssetList)
    {
        List<Asset> assets = new();
        for (int i = 0; i < p_allAssetList.Length; i++)
        {
            string symbol = p_allAssetList[i];
            string sqTicker = (symbol[0] == '^') ? $"I/{symbol[1..]}" : $"S/{symbol}";   // ^ prefix in symbol means, it is in index, such as "^VIX".
            Asset? asset = MemDb.gMemDb.AssetsCache.TryGetAsset(sqTicker);
            if (asset != null)
                assets.Add(asset);
        }

        DateTime nowET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
        // PctChannel needs 252 days and we need another extra 30 trading days rolling window to calculate PctChannels during the previous lookback window
        // PctChannel Signal cannot be calculated just having the last day data, because it has to be rolled further. As it can exit/enter into bullish signals along the way of the simulation.
        // Estimated needed 252 trading days = 365 calendar days.
        // And an additional rolling window of 30 trading days (at least). That is another 45 calendar days.
        // As minimal, we need 365 + 45 = 410 calendar days.
        // For more robust calculations, we can use a 6 month rolling window. That is 120 trading days = 185 calendar days. Altogether: 365+185 = 550
        // DateTime startIncLoc = nowET.AddDays(-408); // This can reproduce the old SqLab implementation with 33 days rolling simulation window
        DateTime startIncLoc = nowET.AddDays(-490);    // This uses a 6-months, 120 trading days rolling simulation window for PctChannels

        List<(Asset asset, List<AssetHistValue> values)> assetHistsAndEst = MemDb.gMemDb.GetSdaHistClosesAndLastEstValue(assets, startIncLoc, true).ToList();
        List<List<DailyData>> quotesData = new();
        for (int i = 3; i < assetHistsAndEst.Count - 1; i++)
        {
            var vals = assetHistsAndEst[i].values;
            List<DailyData> uberValsData = new();
            for (int j = 0; j < vals.Count; j++)
            {
                uberValsData.Add(new DailyData() { Date = vals[j].Date, AdjClosePrice = vals[j].SdaValue });
            }
            quotesData.Add(uberValsData);
        }

        List<List<DailyData>> quotesForClmtData = new();
        for (int i = 0; i < 3; i++)
        {
            var vals = assetHistsAndEst[i].values;
            List<DailyData> clmtData = new();
            for (int j = 0; j < vals.Count; j++)
            {
                clmtData.Add(new DailyData() { Date = vals[j].Date, AdjClosePrice = vals[j].SdaValue });
            }
            quotesForClmtData.Add(clmtData);
        }
        // last ticker is TLT, which is used as a cash substitute. Special role.
        List<DailyData> cashEquivalentQuotesData = new();
        var cashVals = assetHistsAndEst[^1].values;
        for (int j = 0; j < cashVals.Count; j++)
            cashEquivalentQuotesData.Add(new DailyData() { Date = cashVals[j].Date, AdjClosePrice = cashVals[j].SdaValue });

        return (quotesData, quotesForClmtData, cashEquivalentQuotesData);
    }

    public static Tuple<double[], double[,]> TaaWeights(IList<List<DailyData>> p_taaWeightsData, int[] p_pctChannelLookbackDays, int p_histVolLookbackDays, int p_thresholdLower, bool p_winnerRun)
    {
        var dshd = p_taaWeightsData;
        int nAssets = p_taaWeightsData.Count;

        double[] assetScores = new double[nAssets];
        double[] assetScoresMod = new double[nAssets];
        double[] assetHV = new double[nAssets];
        double[] assetWeights = new double[nAssets];
        double[] assetWeights2 = new double[nAssets];
        double[,] assetPctChannelsUpper = new double[nAssets, p_pctChannelLookbackDays.Length];  // for assets and for each
        double[,] assetPctChannelsLower = new double[nAssets, p_pctChannelLookbackDays.Length];  // for assets and for each
        sbyte[,] assetPctChannelsSignal = new sbyte[nAssets, p_pctChannelLookbackDays.Length];  // for assets and for each
        int startNumDay = p_pctChannelLookbackDays.Max() - 1;
        double thresholdLower = p_thresholdLower / 100.0;
        double thresholdUpper = 1 - thresholdLower;

        int nDays = p_taaWeightsData[0].Count - startNumDay;
        double[,] dailyAssetWeights = new double[nDays, nAssets];
        double[,] dailyAssetScores = new double[nDays, nAssets];
        double[,] dailyAssetScoresMod = new double[nDays, nAssets];
        double[,] dailyAssetHv = new double[nDays, nAssets];
        for (int iDay = 0; iDay < nDays; iDay++)
        {
            for (int iAsset = 0; iAsset < nAssets; iAsset++)
            {
                double assetPrice = p_taaWeightsData[iAsset][startNumDay + iDay].AdjClosePrice;
                for (int iChannel = 0; iChannel < p_pctChannelLookbackDays.Length; iChannel++)
                {
                    // A long position would be initiated if the price exceeds the 75th percentile of prices over the last “n” days.The position would be closed if the price falls below the 25th percentile of prices over the last “n” days.
                    var usedQuotes = p_taaWeightsData[iAsset].GetRange(startNumDay + iDay - (p_pctChannelLookbackDays[iChannel] - 1), p_pctChannelLookbackDays[iChannel]).Select(r => r.AdjClosePrice);
                    assetPctChannelsLower[iAsset, iChannel] = Statistics.Quantile(usedQuotes, thresholdLower);
                    assetPctChannelsUpper[iAsset, iChannel] = Statistics.Quantile(usedQuotes, thresholdUpper);
                    if (assetPrice < assetPctChannelsLower[iAsset, iChannel])
                    assetPctChannelsSignal[iAsset, iChannel] = -1;
                    else if (assetPrice > assetPctChannelsUpper[iAsset, iChannel])
                    assetPctChannelsSignal[iAsset, iChannel] = 1;
                    else if (iDay == 0)
                    assetPctChannelsSignal[iAsset, iChannel] = 1;
                }
            }

            // Calculate assetWeights
            double totalWeight = 0.0;

            for (int iAsset = 0; iAsset < nAssets; iAsset++)
            {
                sbyte compositeSignal = 0;    // For every stocks, sum up the four signals every day. This sum will be -4, -2, 0, +2 or +4.
                for (int iChannel = 0; iChannel < p_pctChannelLookbackDays.Length; iChannel++)
                {
                    compositeSignal += assetPctChannelsSignal[iAsset, iChannel];
                }
                assetScores[iAsset] = compositeSignal / 4.0;    // Divide it by 4 to get a signal between -1 and +1 (this will be the “score”).
                assetScoresMod[iAsset] = compositeSignal / 8.0 + 0.5;    // Divide it by 4 to get a signal between -1 and +1 (this will be the “score”).

                double[] hvPctChg = new double[p_histVolLookbackDays];
                for (int iHv = 0; iHv < p_histVolLookbackDays; iHv++)
                {
                    hvPctChg[p_histVolLookbackDays - iHv - 1] = p_taaWeightsData[iAsset][startNumDay + iDay - iHv].AdjClosePrice / p_taaWeightsData[iAsset][startNumDay + iDay - iHv - 1].AdjClosePrice - 1;
                }
                // Balazs: uses "corrected sample standard deviation"; corrected: dividing by 19, not 20; He doesn't annualize. He uses daily StDev
                assetHV[iAsset] = ArrayStatistics.StandardDeviation(hvPctChg);  // Calculate the 20-day historical volatility of daily percentage changes for every stock.
                assetWeights[iAsset] = assetScores[iAsset] / assetHV[iAsset];   // “Score/Vol” quotients will define the weights of the stocks. They can be 0 or negative as well.
                                                                                // there is an interesting observation here. Actually, it is a good behavour.
                                                                                // If assetScores[i]=0, assetWeights[i] becomes 0, so we don't use its weight when p_isCashAllocatedForNonActives => TLT will not fill its Cash-place; NO TLT will be invested (if this is the only stock with 0 score), the portfolio will be 100% in other stocks. We are more Brave.
                                                                                // However, if assetScores[i]<0 (negative), assetWeights[i] becoumes a proper negative number. It will be used in TotalWeight calculation => TLT will fill its's space. (if this is the only stock with negative score), TLT will be invested in its place; consequently the portfolio will NOT be 100% in other stocks. We are more defensive.
                totalWeight += Math.Abs(assetWeights[iAsset]);      // Sum up the absolute values of the “Score/Vol” quotients. TotalWeight contains even the non-active assets so have have some cash.
                assetWeights2[iAsset] = (assetWeights[iAsset] >= 0) ? assetWeights[iAsset] : 0.0;
            }
            for (int iAsset = 0; iAsset < nAssets; iAsset++)
            {
                dailyAssetWeights[iDay, iAsset] = assetWeights2[iAsset] / totalWeight;
                dailyAssetScores[iDay, iAsset] = assetScores[iAsset];
                dailyAssetHv[iDay, iAsset] = assetHV[iAsset];
                dailyAssetScoresMod[iDay, iAsset] = assetScoresMod[iAsset];
            }
        }

        IEnumerable<DateTime> taaWeightDateVec = p_taaWeightsData[0].GetRange(p_taaWeightsData[0].Count - nDays, nDays).Select(r => r.Date);
        DateTime[] taaWeightDateArray = taaWeightDateVec.ToArray();
        DateTime startMatlabDate = DateTime.ParseExact("1900/01/01", "yyyy/MM/dd", CultureInfo.InvariantCulture);

        double[] taaWeightMatlabDateVec = new double[taaWeightDateVec.Count()];
        for (int i = 0; i < taaWeightMatlabDateVec.Length; i++)
        {
            taaWeightMatlabDateVec[i] = (taaWeightDateArray[i] - startMatlabDate).TotalDays + 693962;
        }

        Tuple<double[], double[,]> taaWeightResults = p_winnerRun ? Tuple.Create(taaWeightMatlabDateVec, dailyAssetScoresMod) : Tuple.Create(taaWeightMatlabDateVec, dailyAssetWeights);
        return taaWeightResults;
    }

    public static double[][] CLMTCalc(IList<List<DailyData>> p_quotesForClmtData)
    {
        double[,] p_clmtData = new double[p_quotesForClmtData[0].Count, 4];

        IEnumerable<DateTime> clmtDateVec = p_quotesForClmtData[0].Select(r => r.Date);
        DateTime[] clmtDateArray = clmtDateVec.ToArray();
        DateTime startMatlabDate = DateTime.ParseExact("1900/01/01", "yyyy/MM/dd", CultureInfo.InvariantCulture);

        double[] clmtMatlabDateVec = new double[clmtDateVec.Count()];
        for (int i = 0; i < clmtMatlabDateVec.Length; i++)
        {
            clmtMatlabDateVec[i] = (clmtDateArray[i] - startMatlabDate).TotalDays + 693962;
        }

        for (int iRows = 0; iRows < p_clmtData.GetLength(0); iRows++)
        {
            p_clmtData[iRows, 0] = clmtMatlabDateVec[iRows];
            for (int jCols = 0; jCols < p_clmtData.GetLength(1) - 1; jCols++)
            {
                p_clmtData[iRows, jCols + 1] = p_quotesForClmtData[jCols][iRows].AdjClosePrice;
            }
        }


        double[] xluRSI = new double[p_clmtData.GetLength(0) - 200];
        for (int iRows = 0; iRows < xluRSI.Length; iRows++)
        {
            double losses = new();
            double gains = new();
            int lossNum = 0;
            int gainNum = 0;
            for (int kRows = 0; kRows < 20; kRows++)
            {
                if (p_clmtData[iRows + kRows + 181, 2] - p_clmtData[iRows + kRows + 180, 2] >= 0)
                {
                    gains = gains + p_clmtData[iRows + kRows + 181, 2] - p_clmtData[iRows + kRows + 180, 2];
                    gainNum += 1;
                }
                else
                {
                    losses = losses + p_clmtData[iRows + kRows + 181, 2] - p_clmtData[iRows + kRows + 180, 2];
                    lossNum += 1;
                }
            }
            xluRSI[iRows] = 100 - 100 * (-losses / (-losses + gains));

        }

        double[] vtiRSI = new double[p_clmtData.GetLength(0) - 200];
        for (int iRows = 0; iRows < vtiRSI.Length; iRows++)
        {
            double losses = new();
            double gains = new();
            for (int kRows = 0; kRows < 20; kRows++)
            {
                if (p_clmtData[iRows + kRows + 181, 3] - p_clmtData[iRows + kRows + 180, 3] >= 0)
                {
                    gains = gains + p_clmtData[iRows + kRows + 181, 3] - p_clmtData[iRows + kRows + 180, 3];
                }
                else
                {
                    losses = losses + p_clmtData[iRows + kRows + 181, 3] - p_clmtData[iRows + kRows + 180, 3];
                }
            }
            vtiRSI[iRows] = 100 - 100 * (-losses / (-losses + gains));
        }

        double[] xluVtiIndi = new double[xluRSI.Length];
        for (int iRows = 0; iRows < xluVtiIndi.Length; iRows++)
        {
            xluVtiIndi[iRows] = (xluRSI[iRows] >= vtiRSI[iRows]) ? 2 : 1;
        }

        double[] spxMA50 = new double[p_clmtData.GetLength(0) - 200];
        double[] spxPrice = new double[p_clmtData.GetLength(0) - 200];
        for (int iRows = 0; iRows < spxMA50.Length; iRows++)
        {
            spxPrice[iRows] = p_clmtData[iRows + 200, 1];
            double sumsSPX50 = new();

            for (int kRows = 0; kRows < 50; kRows++)
            {
                sumsSPX50 += p_clmtData[iRows + kRows + 151, 1];
            }
            spxMA50[iRows] = sumsSPX50 / 50;
        }

        double[] spxMA200 = new double[p_clmtData.GetLength(0) - 200];
        for (int iRows = 0; iRows < spxMA200.Length; iRows++)
        {
            double sumsSPX200 = new();

            for (int kRows = 0; kRows < 200; kRows++)
            {
                sumsSPX200 += p_clmtData[iRows + kRows + 1, 1];
            }
            spxMA200[iRows] = sumsSPX200 / 200;
        }

        double[] spxMAIndi = new double[spxMA50.Length];
        for (int iRows = 0; iRows < spxMAIndi.Length; iRows++)
        {
            spxMAIndi[iRows] = (spxMA50[iRows] >= spxMA200[iRows]) ? 1 : 0;
        }

        double[] clmtIndi = new double[spxMAIndi.Length];
        for (int iRows = 0; iRows < clmtIndi.Length; iRows++)
        {
            if (spxMAIndi[iRows] == 1 & xluVtiIndi[iRows] == 1)
            {
                clmtIndi[iRows] = 1;
            }
            else if (spxMAIndi[iRows] == 0 & xluVtiIndi[iRows] == 2)
            {
                clmtIndi[iRows] = 3;
            }
            else
            {
                clmtIndi[iRows] = 2;
            }
        }

        double[] clmtDateVec2 = new double[clmtIndi.Length];
        for (int iRows = 0; iRows < clmtDateVec2.Length; iRows++)
        {
            clmtDateVec2[iRows] = p_clmtData[iRows + 200, 0];
        }

        double[][] clmtTotalResu = new double[9][];
        clmtTotalResu[0] = clmtDateVec2;
        clmtTotalResu[1] = xluRSI;
        clmtTotalResu[2] = vtiRSI;
        clmtTotalResu[3] = xluVtiIndi;
        clmtTotalResu[4] = spxMA50;
        clmtTotalResu[5] = spxMA200;
        clmtTotalResu[6] = spxMAIndi;
        clmtTotalResu[7] = clmtIndi;
        clmtTotalResu[8] = spxPrice;

    // StringBuilder stringBuilder=new StringBuilder();
    //     foreach (var item in clmtTotalResu)
    //     {
    //         foreach (var item2 in item)
    //         {
    //             stringBuilder.Append(item2 + ",");
    //         }
    //         stringBuilder.AppendLine("ß" + Environment.NewLine + Environment.NewLine);
    //     }

    // System.IO.File.WriteAllText(@"D:\xxx.csv", stringBuilder.ToString());

        return clmtTotalResu;
    }

    public static Tuple<double[,], double[,], double[,], string[], string[]> MultiplFinCalc(double[][] p_clmtRes, Tuple<double[], int[,], int[], int[], string[], int[], int[]> p_gSheetResToFinCalc, string[] p_allAssetList, double p_lastDataDate, Tuple<double[], double[,]> p_taaWeightResultsTuple)
    {
        int pastDataLength = 20;
        int futDataLength = 10;
        int indClmtRes = Array.IndexOf(p_clmtRes[0], p_lastDataDate);
        int indGSheetRes = Array.IndexOf(p_gSheetResToFinCalc.Item1, p_lastDataDate);
        int indWeightsRes = Array.IndexOf(p_taaWeightResultsTuple.Item1, p_lastDataDate);

        double[,] pastCodes = new double[pastDataLength, p_allAssetList.Length - 3];
        double[,] futCodes = new double[futDataLength, p_allAssetList.Length - 3];
        string[] pastEvents = new string[pastDataLength];
        string[] futEvents = new string[futDataLength];

        for (int iRows = 0; iRows < pastCodes.GetLength(0); iRows++)
        {
            pastEvents[iRows] = p_gSheetResToFinCalc.Item5[indGSheetRes - pastDataLength + iRows + 2];
            pastCodes[iRows, 0] = p_gSheetResToFinCalc.Item1[indGSheetRes - pastDataLength + iRows + 2];
            for (int jCols = 1; jCols < pastCodes.GetLength(1); jCols++)
            {
                if (p_gSheetResToFinCalc.Item2[indGSheetRes - pastDataLength + iRows + 2, jCols - 1] == 9)
                {
                    pastCodes[iRows, jCols] = 7;
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows + 2] == 1)
                {
                    pastCodes[iRows, jCols] = 1;
                }
                else if (p_gSheetResToFinCalc.Item2[indGSheetRes - pastDataLength + iRows + 2, jCols - 1] == 3)
                {
                    pastCodes[iRows, jCols] = 5;
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows + 2] == 2)
                {
                    pastCodes[iRows, jCols] = 2;
                }
                else if (p_gSheetResToFinCalc.Item2[indGSheetRes - pastDataLength + iRows + 2, jCols - 1] == 1)
                {
                    if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows + 2] == 3)
                    {
                        pastCodes[iRows, jCols] = 3;
                    }
                    else
                    {
                        pastCodes[iRows, jCols] = 6;
                    }
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows + 2] == 3)
                {
                    pastCodes[iRows, jCols] = 3;
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows + 2] == 4)
                {
                    pastCodes[iRows, jCols] = 4;
                }
                else if (p_clmtRes[7][indClmtRes - pastDataLength + iRows + 1] == 1)
                {
                    pastCodes[iRows, jCols] = 8;
                }
                else if (p_clmtRes[7][indClmtRes - pastDataLength + iRows + 1] == 2)
                {
                    pastCodes[iRows, jCols] = 9;
                }
                else if (p_clmtRes[7][indClmtRes - pastDataLength + iRows + 1] == 3)
                {
                    pastCodes[iRows, jCols] = 10;
                }
            }
        }

        for (int iRows = 0; iRows < futCodes.GetLength(0); iRows++)
        {
            futEvents[iRows] = p_gSheetResToFinCalc.Item5[indGSheetRes + iRows + 2];
            futCodes[iRows, 0] = p_gSheetResToFinCalc.Item1[indGSheetRes + iRows + 2];
            for (int jCols = 1; jCols < futCodes.GetLength(1); jCols++)
            {
                if (p_gSheetResToFinCalc.Item2[indGSheetRes + iRows + 2, jCols - 1] == 9)
                {
                    futCodes[iRows, jCols] = 7;
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows + 2] == 1)
                {
                    futCodes[iRows, jCols] = 1;
                }
                else if (p_gSheetResToFinCalc.Item2[indGSheetRes + iRows + 2, jCols - 1] == 3)
                {
                    futCodes[iRows, jCols] = 5;
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows + 2] == 2)
                {
                    futCodes[iRows, jCols] = 2;
                }
                else if (p_gSheetResToFinCalc.Item2[indGSheetRes + iRows + 2, jCols - 1] == 1)
                {
                    if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows + 2] == 3)
                    {
                        futCodes[iRows, jCols] = 3;
                    }
                    else
                    {
                        futCodes[iRows, jCols] = 6;
                    }
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows + 2] == 3)
                {
                    futCodes[iRows, jCols] = 3;
                }
                else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows + 2] == 4)
                {
                    futCodes[iRows, jCols] = 4;
                }
                else
                {
                    futCodes[iRows, jCols] = 11;
                }
            }
        }

        double[,] pastWeightsFinal = new double[pastCodes.GetLength(0), p_allAssetList.Length - 3];
        double numAss = Convert.ToDouble(p_allAssetList.Length - 4);
        for (int iRows = 0; iRows < pastWeightsFinal.GetLength(0); iRows++)
        {
            pastWeightsFinal[iRows, 0] = pastCodes[iRows, 0];
            for (int jCols = 1; jCols < pastWeightsFinal.GetLength(1); jCols++)
            {
                if (pastCodes[iRows, jCols] == 7)
                {
                    pastWeightsFinal[iRows, jCols] = 0;
                }
                else if (pastCodes[iRows, jCols] == 1)
                {
                    pastWeightsFinal[iRows, jCols] = 1.75 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
                }
                else if (pastCodes[iRows, jCols] == 5)
                {
                    pastWeightsFinal[iRows, jCols] = Math.Max(1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1], 1 / numAss);
                }
                else if (pastCodes[iRows, jCols] == 2)
                {
                    pastWeightsFinal[iRows, jCols] = 0;
                }
                else if (pastCodes[iRows, jCols] == 3)
                {
                    pastWeightsFinal[iRows, jCols] = 1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
                }
                else if (pastCodes[iRows, jCols] == 6)
                {
                    pastWeightsFinal[iRows, jCols] = Math.Max(1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1], 1 / numAss);
                    // pastWeightsFinal[iRows, jCols] = Math.Max(1.25 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1], 1 / numAss); #Mr.C. decided to increase leverage to 50% on bullish days
                }
                else if (pastCodes[iRows, jCols] == 4)
                {
                    pastWeightsFinal[iRows, jCols] = 0;
                }
                else if (pastCodes[iRows, jCols] == 8)
                {
                    pastWeightsFinal[iRows, jCols] = 1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
                    // pastWeightsFinal[iRows, jCols] = 1.2 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1]; #Mr.C. decided to increase leverage to 50% on bullish days
                }
                else if (pastCodes[iRows, jCols] == 9)
                {
                    pastWeightsFinal[iRows, jCols] = 1 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
                    // pastWeightsFinal[iRows, jCols] = 0.8 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
                }
                else if (pastCodes[iRows, jCols] == 10)
                {
                    pastWeightsFinal[iRows, jCols] = 0.6 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
                    // pastWeightsFinal[iRows, jCols] = 0.4 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
                }
            }
        }

        Tuple<double[,], double[,], double[,], string[], string[]> multiplFinResults = Tuple.Create(pastCodes, futCodes, pastWeightsFinal, pastEvents, futEvents);

        return multiplFinResults;
    }

    public static string[]? GetTickersFromGSheet(string? p_gSheetStr)
    {
        if (p_gSheetStr == null)
            return null;

        if (p_gSheetStr.StartsWith("Error"))
            return null;

        int tickerStartIdx = p_gSheetStr.IndexOf("CDate\",");
        if (tickerStartIdx < 0)
            return null;
        int tickerEndIdx = p_gSheetStr.IndexOf("Cash\",", tickerStartIdx + 1);
        if (tickerEndIdx < 0)
            return null;
        string tickers = p_gSheetStr.Substring(tickerStartIdx + 5, tickerEndIdx - tickerStartIdx - 6);
        return tickers.Split(new string[] { ",\n", "\"" }, StringSplitOptions.RemoveEmptyEntries).Where(x => !string.IsNullOrWhiteSpace(x.Trim())).ToArray();
    }
}