using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SqCommon;
using FinTechCommon;
using System.Text;
using System.Globalization;
using MathCommon.MathNet;

namespace SqCoreWeb.Controllers;

[ApiController]
[Route("[controller]")]
[ResponseCache(CacheProfileName = "NoCache")]
public class VolatilityDragVisualizerController : ControllerBase
{
    public class DailyData
    {
        public DateTime Date { get; set; }
        public double AdjClosePrice { get; set; }
    }

    [HttpGet]
    public ActionResult Index(String commo)
    {
        if (int.TryParse(commo, out int lbP) && lbP > 1)
        {
            try
            {
                return Content(Get(lbP), "text/html");
            }
            catch
            {
                return Content(GetStr2(), "text/html");
            }
        }
        else if (commo == "JUVE")
        {
            try
            {
                return Content(Get(20), "text/html");
            }
            catch
            {
                return Content(GetStr2(), "text/html");
            }
        }
        else
        {
            return Content(GetStr2(), "text/html");
        }
    }

    public static string GetStr2()
    {
        return "Error";
    }

        // Downloading price data from SQL Server
    public static IList<List<DailyData>> GetVolatilityStockHistData(string[] p_allAssetList)
    {
        Utils.Logger.Info("DataSQDBGmod() START");
        List<Asset> assets = new();
        for (int i = 0; i < p_allAssetList.Length; i++)
        {
            string symbol = p_allAssetList[i];
            string sqTicker = (symbol[0] == '^') ? $"I/{symbol[1..]}" : $"S/{symbol}";   // ^ prefix in symbol means, it is in index, such as "^VIX".
            Asset? asset = MemDb.gMemDb.AssetsCache.TryGetAsset(sqTicker);
            if (asset != null)
                assets.Add(asset);
        }

        // DateTime nowET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
        DateTime startIncLoc = DateTime.ParseExact("2004/03/26", "yyyy/MM/dd", CultureInfo.InvariantCulture);
        // DateTime startIncLoc = nowET.AddDays(-550);

        List<List<DailyData>> volatilityTickersData = new();
        // List<DailyData> VIXDailyquotes = new();

        List<(Asset asset, List<AssetHistValue> values)> assetHistsAndEst = MemDb.gMemDb.GetSdaHistClosesAndLastEstValue(assets, startIncLoc, true).ToList();
        for (int i = 0; i < assetHistsAndEst.Count; i++)
        {
            var vals = assetHistsAndEst[i].values;
            List<DailyData> uberValsData = new();
            for (int j = 0; j < vals.Count; j++)
            {
                uberValsData.Add(new DailyData() { Date = vals[j].Date, AdjClosePrice = vals[j].SdaValue });
            }
            volatilityTickersData.Add(uberValsData);
        }

        Utils.Logger.Info("DataSQDBGmod() END");
        return volatilityTickersData;
    }

    public static string Get(int p_lbP)
    {
        // //Defining asset lists.

        string[] volAssetList = new string[] { "SVXY", "VXX", "VXZ" };
        string[] volAssetListNN = new string[] { "SVXY", "VXX", "VXZ" };

        string[] etpAssetList = new string[] { "SPY", "UPRO", "QQQ", "TQQQ", "TLT", "TMV", "USO", "UNG" };
        string[] etpAssetListNN = new string[] { "SPY", "UPRO", "QQQ", "TQQQ", "TLT", "TMV", "USO", "UNG" };

        string[] gchAssetList = new string[] { "AAPL", "AMZN", "GOOGL" };
        string[] gchAssetListNN = new string[] { "AAPL", "AMZN", "GOOGL" };

        string[] gmAssetList = new string[] { "EEM", "VNQ"};
        string[] gmAssetListNN = new string[] { "EEM", "VNQ" };

        string[] vixAssetList = new string[] { "^VIX" };

        // string[] defaultCheckedList = new string[] { "SPY", "QQQ", "TQQQ", "VXX", "TMV", "UCO", "UNG" };
        // string[] volAssetList = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };
        // string[] volAssetListNN = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };
        // string[] etpAssetList = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };
        // string[] etpAssetListNN = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };

        // string[] gchAssetList = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };
        // string[] gchAssetListNN = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };

        // string[] gmAssetList = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };
        // string[] gmAssetListNN = new string[] { "SPY", "QQQ", "TLT", "UNG", "USO", "GLD" };

        // string[] vixAssetList = new string[] { "^VIX" };

        var allAssetList = etpAssetList.Union(volAssetList).Union(gchAssetList).Union(gmAssetList).Union(vixAssetList).ToArray();
        var usedAssetList = etpAssetListNN.Union(volAssetListNN).Union(gchAssetListNN).Union(gmAssetListNN).ToArray();
        // var allAssetList = etpAssetList.Union(vixAssetList).ToArray();
        // var usedAssetList = etpAssetListNN.ToArray();
        // string[] allAssetList = new string[]{ "SPY", "QQQ", "SVXY!Light0.5x.SQ", "VXX.SQ", "VXZ.SQ", "UVXY!Light1.5x.SQ", "TQQQ.SQ", "^VIX" };
        // string[] usedAssetList = new string[] { "SPY", "QQQ", "SVXY_Light", "VXX", "VXZ", "UVXY_Light", "TQQQ"};
        // string[] defaultCheckedList = new string[] { "SPY", "QQQ", "VXX"};
        string[] defaultCheckedList = new string[] { "SPY", "QQQ", "TLT"};

        int volLBPeriod = p_lbP;
        int[] retLB = new int[] {1, 3, 5, 10, 20, 63, 126, 252};
        string[] retLBStr = new string[] { "1 Day", "3 Days", "1 Week", "2 Weeks", "1 Month", "3 Months", "6 Months", "1 Year" };
        int retHistLB = 20;

        // Collecting and splitting price data got from SQL Server
        IList<List<DailyData>> quotesData = GetVolatilityStockHistData(allAssetList);
        IList<List<DailyData>> quotesData1 = new List<List<DailyData>>(quotesData);
        quotesData1.RemoveAt(allAssetList.Length - 1);

        List<DailyData> quotesData2 = quotesData[allAssetList.Length - 1];

        int noAssets = allAssetList.Length - 1;
        int noBtDays = quotesData1[0].Count;
        DateTime[] quotesDateVec = new DateTime[noBtDays];

        for (int iRows = 0; iRows < quotesDateVec.Length; iRows++)
        {
            quotesDateVec[iRows] = quotesData1[0][iRows].Date;
        }

        DateTime[] quotesFirstDates = new DateTime[noAssets];

        for (int jAssets = 0; jAssets < quotesFirstDates.Length; jAssets++)
        {
            quotesFirstDates[jAssets] = quotesData1[jAssets][0].Date;
        }

        DateTime[] quotesLastDates = new DateTime[noAssets];

        for (int jAssets = 0; jAssets < quotesLastDates.Length; jAssets++)
        {
            quotesLastDates[jAssets] = quotesData1[jAssets][^1].Date;
        }

        double[] quotesFirstPrices = new double[noAssets];

        for (int jAssets = 0; jAssets < quotesFirstPrices.Length; jAssets++)
        {
            quotesFirstPrices[jAssets] = quotesData1[jAssets][0].AdjClosePrice;
        }

        double[] quotesLastPrices = new double[noAssets];

        for (int jAssets = 0; jAssets < quotesLastPrices.Length; jAssets++)
        {
            quotesLastPrices[jAssets] = quotesData1[jAssets][^1].AdjClosePrice;
        }

        IList<List<double>> quotesPrices = new List<List<double>>();

        for (int iAsset = 0; iAsset < noAssets; iAsset++)
        {
            int shiftDays = 0;
            List<double> assPriceSubList = new();
            // for (int jRows = 0; jRows < noBtDays; jRows++)
            // {
            int jRows = 0;
            while (quotesDateVec[jRows] < quotesFirstDates[iAsset])
            {
                assPriceSubList.Add(quotesFirstPrices[iAsset]);
                shiftDays += 1;
                jRows++;
                if (jRows >= noBtDays)
                {
                    break;
                }
            }
            while (quotesDateVec[jRows] == quotesData1[iAsset][jRows - shiftDays].Date)
            {
                assPriceSubList.Add(quotesData1[iAsset][jRows - shiftDays].AdjClosePrice);
                jRows++;
                if (jRows >= quotesData1[iAsset].Count + shiftDays)
                {
                    break;
                }
            }
            if (jRows < noBtDays)
            {
                while (quotesDateVec[jRows] > quotesLastDates[iAsset])
                {
                    assPriceSubList.Add(quotesLastPrices[iAsset]);
                    jRows++;
                    if (jRows >= noBtDays)
                    {
                        break;
                    }
                }
            }
            // }
            quotesPrices.Add(assPriceSubList);
        }

        double[,] histRet = new double[retLB.Length, noAssets];

        for (int iAsset = 0; iAsset < noAssets; iAsset++)
        {
            for (int jRows = 0; jRows < retLB.Length; jRows++)
            {
                histRet[jRows, iAsset] = quotesPrices[iAsset][quotesPrices[0].Count - 1] / quotesPrices[iAsset][quotesPrices[0].Count - 1 - retLB[jRows]] - 1;
            }
        }

        int histRetLengthSum = retLB.Sum();
        double[,] histRet2 = new double[histRetLengthSum, noAssets];

        int kShift = 0;
        for (int kLen = 0; kLen < retLB.Length; kLen++)
        {
            for (int iAsset = 0; iAsset < noAssets; iAsset++)
            {
                for (int jRows = 0; jRows < retLB[kLen]; jRows++)
                {
                    histRet2[kShift + jRows, iAsset] = quotesPrices[iAsset][quotesPrices[0].Count - retLB[kLen] + jRows] / quotesPrices[iAsset][quotesPrices[0].Count - 1 - retLB[kLen]] - 1;
                }
            }
            kShift += retLB[kLen];
        }

        IList<List<double>> quotesRets = new List<List<double>>();

        for (int iAsset = 0; iAsset < noAssets; iAsset++)
        {
            List<double> assSubList = new();
            assSubList.Add(0);
            for (int jRows = 1; jRows < noBtDays; jRows++)
            {
                assSubList.Add(quotesPrices[iAsset][jRows] / quotesPrices[iAsset][jRows - 1] - 1);
            }
            quotesRets.Add(assSubList);
        }

        IList<List<double>> assVolDrags = new List<List<double>>();
        List<double> vixQuotes = new();
        double[] vixLevel = new double[noBtDays];
        if (quotesData2.Count < noBtDays)
        {
            quotesData2.Add(quotesData2[^1]);
        }

        for (int iRows = 0; iRows < noBtDays; iRows++)
        {
            vixQuotes.Add(quotesData2[iRows].AdjClosePrice);
        }

        for (int iAsset = 0; iAsset < noAssets; iAsset++)
        {
            List<double> assVolDragSubList = new();
            for (int jRows = 0; jRows < volLBPeriod - 1; jRows++)
            {
                assVolDragSubList.Add(0);
                vixLevel[jRows] = Math.Round(ArrayStatistics.Mean(vixQuotes.GetRange(0, jRows).ToArray()), 3);
            }
            for (int jRows = volLBPeriod - 1; jRows < noBtDays; jRows++)
            {
                assVolDragSubList.Add(ArrayStatistics.Variance(quotesRets[iAsset].GetRange(jRows - volLBPeriod + 1, volLBPeriod).ToArray()) / 2 * 21);
                vixLevel[jRows] = Math.Round(ArrayStatistics.Mean(vixQuotes.GetRange(jRows - volLBPeriod + 1, volLBPeriod).ToArray()), 3);
            }
            assVolDrags.Add(assVolDragSubList);
        }
        vixLevel[0] = quotesData2[0].AdjClosePrice;

        string[] dateYearsVec = new string[noBtDays];
        string[] dateYearsMonthsVec = new string[noBtDays];
        for (int iRows = 0; iRows < dateYearsMonthsVec.Length; iRows++)
        {
            dateYearsVec[iRows] = quotesDateVec[iRows].ToString("yyyy");
            dateYearsMonthsVec[iRows] = quotesDateVec[iRows].ToString("yyyy-MM");
        }

        // Tuple<string[], string[], IList<List<double>>> dataToCumm = Tuple.Create(dateYearsVec, dateYearsMonthsVec, assVolDrags);

        string[] dateYearsDist = dateYearsVec.Distinct().ToArray();
        string[] dateYearsMonthsDist = dateYearsMonthsVec.Distinct().ToArray();

        double[,] dateYearsAvgs = new double[dateYearsDist.Length, noAssets];
        double[] dateYearsVixAvgs = new double[dateYearsDist.Length];
        int[] dateYearsCount = new int[dateYearsDist.Length];
        int kElem = 0;
        for (int iRows = 0; iRows < dateYearsDist.Length; iRows++)
        {
            double[] subSumVec = new double[noAssets];
            double subSumVix = 0;
            while (kElem < noBtDays && dateYearsVec[kElem] == dateYearsDist[iRows])
            {
                for (int jAssets = 0; jAssets < noAssets; jAssets++)
                {
                    subSumVec[jAssets] = subSumVec[jAssets] + assVolDrags[jAssets][kElem];
                }
                subSumVix += vixLevel[kElem];
                kElem++;
                dateYearsCount[iRows] += 1;
            }
            for (int jAssets = 0; jAssets < noAssets; jAssets++)
            {
                dateYearsAvgs[iRows, jAssets] = subSumVec[jAssets] / dateYearsCount[iRows];
            }
            dateYearsVixAvgs[iRows] = subSumVix / dateYearsCount[iRows];
        }
        int noTotalDays = dateYearsCount.Sum();

        double[,] dateYearsMonthsAvgs = new double[dateYearsMonthsDist.Length, noAssets];
        double[] dateYearsMonthsVixAvgs = new double[dateYearsMonthsDist.Length];
        int[] dateYearsMonthsCount = new int[dateYearsMonthsDist.Length];
        int kElemM = 0;
        for (int iRows = 0; iRows < dateYearsMonthsDist.Length; iRows++)
        {
            double[] subSumVec = new double[noAssets];
            double subSumVix = 0;
            while (kElemM < noBtDays && dateYearsMonthsVec[kElemM] == dateYearsMonthsDist[iRows])
            {
                for (int jAssets = 0; jAssets < noAssets; jAssets++)
                {
                    subSumVec[jAssets] = subSumVec[jAssets] + assVolDrags[jAssets][kElemM];
                }
                subSumVix += vixLevel[kElemM];
                kElemM++;
                dateYearsMonthsCount[iRows] += 1;
            }
            for (int jAssets = 0; jAssets < noAssets; jAssets++)
            {
                dateYearsMonthsAvgs[iRows, jAssets] = subSumVec[jAssets] / dateYearsMonthsCount[iRows];
            }
            dateYearsMonthsVixAvgs[iRows] = subSumVix / dateYearsMonthsCount[iRows];
        }

        double vixAvgTotal = ArrayStatistics.Mean(vixLevel);
        double[] volDragsAvgsTotal = new double[noAssets];
        for (int jAssets = 0; jAssets < noAssets; jAssets++)
        {
            int numEl = 0;
            double subSum = 0;
            for (int iRows = 0; iRows < noBtDays; iRows++)
                if (assVolDrags[jAssets][iRows] > 0)
                {
                    subSum += assVolDrags[jAssets][iRows];
                    numEl += 1;
                }

            volDragsAvgsTotal[jAssets] = subSum / numEl;
        }

        // Request time (UTC)
        DateTime liveDateTime = DateTime.UtcNow;
        string liveDate = liveDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        DateTime timeNowET = Utils.ConvertTimeFromUtcToEt(liveDateTime);
        string liveDateString = "Request time (UTC): " + liveDate;

        // Last data time (UTC)
        string lastDataTime = (quotesData[0][^1].Date.Date == liveDateTime.Date & timeNowET.TimeOfDay <= new DateTime(2000, 1, 1, 16, 15, 0).TimeOfDay) ? "Live data at " + liveDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "Close price on " + quotesData[0][^1].Date.ToString("yyyy-MM-dd");
        string lastDataTimeString = "Last data time (UTC): " + lastDataTime;

        // Creating input string for JavaScript.
        StringBuilder sb = new ("{" + Environment.NewLine);
        sb.Append(@"""requestTime"": """ + liveDateString);
        sb.Append(@"""," + Environment.NewLine + @"""lastDataTime"": """ + lastDataTimeString);

        sb.Append(@"""," + Environment.NewLine + @"""volLBPeri"": """ + volLBPeriod);
        sb.Append(@"""," + Environment.NewLine + @"""retHistLBPeri"": """ + retHistLB);

        sb.Append(@"""," + Environment.NewLine + @"""retLBPeris"": """);
        for (int i = 0; i < retLB.Length - 1; i++)
            sb.Append(retLBStr[i] + ", ");
        sb.Append(retLBStr[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""retLBPerisNo"": """);
        for (int i = 0; i < retLB.Length - 1; i++)
            sb.Append(retLB[i] + ", ");
        sb.Append(retLB[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""assetNames"": """);
        for (int i = 0; i < usedAssetList.Length - 1; i++)
            sb.Append(usedAssetList[i] + ", ");
        sb.Append(usedAssetList[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""defCheckedList"": """);
        for (int i = 0; i < defaultCheckedList.Length - 1; i++)
            sb.Append(defaultCheckedList[i] + ", ");
        sb.Append(defaultCheckedList[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""volAssetNames"": """);
        for (int i = 0; i < volAssetListNN.Length - 1; i++)
            sb.Append(volAssetListNN[i] + ", ");
        sb.Append(volAssetListNN[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""etpAssetNames"": """);
        for (int i = 0; i < etpAssetListNN.Length - 1; i++)
            sb.Append(etpAssetListNN[i] + ", ");
        sb.Append(etpAssetListNN[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""gchAssetNames"": """);
        for (int i = 0; i < gchAssetListNN.Length - 1; i++)
            sb.Append(gchAssetListNN[i] + ", ");
        sb.Append(gchAssetListNN[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""gmAssetNames"": """);
        for (int i = 0; i < gmAssetListNN.Length - 1; i++)
            sb.Append(gmAssetListNN[i] + ", ");
        sb.Append(gmAssetListNN[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""quotesDateVector"": """);
        for (int i = 0; i < quotesDateVec.Length - 1; i++)
            sb.Append(quotesDateVec[i].ToString("yyyy-MM-dd") + ", ");
        sb.Append(quotesDateVec[^1].ToString("yyyy-MM-dd"));

        sb.Append(@"""," + Environment.NewLine + @"""dailyVolDrags"": """);
        for (int i = 0; i < assVolDrags[0].Count; i++)
        {
            sb.Append("");
            for (int j = 0; j < assVolDrags.Count - 1; j++)
            {
                sb.Append(Math.Round(assVolDrags[j][i] * 100, 2).ToString() + "%, ");
            }
            sb.Append(Math.Round(assVolDrags[assVolDrags.Count - 1][i] * 100, 2).ToString() + "%");
            if (i < assVolDrags[0].Count - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""dailyVIXMas"": """);
        for (int i = 0; i < vixLevel.Length - 1; i++)
            sb.Append(Math.Round(vixLevel[i], 2).ToString() + ", ");
        sb.Append(Math.Round(vixLevel[^1], 2));

        sb.Append(@"""," + Environment.NewLine + @"""yearList"": """);
        for (int i = 0; i < dateYearsDist.Length - 1; i++)
            sb.Append(dateYearsDist[i] + ", ");
        sb.Append(dateYearsDist[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""yearMonthList"": """);
        for (int i = 0; i < dateYearsMonthsDist.Length - 1; i++)
            sb.Append(dateYearsMonthsDist[i] + ", ");
        sb.Append(dateYearsMonthsDist[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""yearlyAvgs"": """);
        for (int i = 0; i < dateYearsAvgs.GetLength(0); i++)
        {
            sb.Append("");
            for (int j = 0; j < dateYearsAvgs.GetLength(1) - 1; j++)
            {
                sb.Append(Math.Round(dateYearsAvgs[i, j] * 100, 2).ToString() + "%, ");
            }
            sb.Append(Math.Round(dateYearsAvgs[i, dateYearsAvgs.GetLength(1) - 1] * 100, 2).ToString() + "%");
            if (i < dateYearsAvgs.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""monthlyAvgs"": """);
        for (int i = 0; i < dateYearsMonthsAvgs.GetLength(0); i++)
        {
            sb.Append("");
            for (int j = 0; j < dateYearsMonthsAvgs.GetLength(1) - 1; j++)
            {
                sb.Append(Math.Round(dateYearsMonthsAvgs[i, j] * 100, 2).ToString() + "%, ");
            }
            sb.Append(Math.Round(dateYearsMonthsAvgs[i, dateYearsMonthsAvgs.GetLength(1) - 1] * 100, 2).ToString() + "%");
            if (i < dateYearsMonthsAvgs.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""yearlyVIXAvgs"": """);
        for (int i = 0; i < dateYearsVixAvgs.Length - 1; i++)
            sb.Append(Math.Round(dateYearsVixAvgs[i], 2).ToString() + ", ");
        sb.Append(Math.Round(dateYearsVixAvgs[^1], 2));

        sb.Append(@"""," + Environment.NewLine + @"""monthlyVIXAvgs"": """);
        for (int i = 0; i < dateYearsMonthsVixAvgs.Length - 1; i++)
            sb.Append(Math.Round(dateYearsMonthsVixAvgs[i], 2).ToString() + ", ");
        sb.Append(Math.Round(dateYearsMonthsVixAvgs[^1], 2));

        sb.Append(@"""," + Environment.NewLine + @"""yearlyCounts"": """);
        for (int i = 0; i < dateYearsCount.Length - 1; i++)
            sb.Append(dateYearsCount[i].ToString() + ", ");
        sb.Append(dateYearsCount[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""noTotalDays"": """ + noTotalDays);

        sb.Append(@"""," + Environment.NewLine + @"""monthlyCounts"": """);
        for (int i = 0; i < dateYearsMonthsCount.Length - 1; i++)
            sb.Append(dateYearsMonthsCount[i].ToString() + ", ");
        sb.Append(dateYearsMonthsCount[^1]);

        sb.Append(@"""," + Environment.NewLine + @"""vixAvgTotal"": """ + Math.Round(vixAvgTotal, 2).ToString());

        sb.Append(@"""," + Environment.NewLine + @"""volDragsAvgsTotalVec"": """);
        for (int i = 0; i < volDragsAvgsTotal.Length - 1; i++)
            sb.Append(Math.Round(volDragsAvgsTotal[i] * 100, 2).ToString() + "%, ");
        sb.Append(Math.Round(volDragsAvgsTotal[^1] * 100, 2).ToString() + "%");

        sb.Append(@"""," + Environment.NewLine + @"""histRetMtx"": """);
        for (int i = 0; i < histRet.GetLength(0); i++)
        {
            sb.Append("");
            for (int j = 0; j < histRet.GetLength(1) - 1; j++)
            {
                sb.Append(Math.Round(histRet[i, j] * 100, 2).ToString() + "%, ");
            }
            sb.Append(Math.Round(histRet[i, histRet.GetLength(1) - 1] * 100, 2).ToString() + "%");
            if (i < histRet.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.Append(@"""," + Environment.NewLine + @"""histRet2Chart"": """);
        for (int i = 0; i < histRet2.GetLength(0); i++)
        {
            sb.Append("");
            for (int j = 0; j < histRet2.GetLength(1) - 1; j++)
            {
                sb.Append(Math.Round(histRet2[i, j] * 100, 2).ToString() + "%, ");
            }
            sb.Append(Math.Round(histRet2[i, histRet2.GetLength(1) - 1] * 100, 2).ToString() + "%");
            if (i < histRet2.GetLength(0) - 1)
            {
                sb.Append("ß ");
            }
        }

        sb.AppendLine(@"""" + Environment.NewLine + @"}");

        return sb.ToString();
    }
}