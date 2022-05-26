using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SqCommon;
using FinTechCommon;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Diagnostics;

namespace SqCoreWeb.Controllers
{    
    [ApiController]
    [Route("[controller]")]
    [ResponseCache(CacheProfileName = "NoCache")]
    public class StrategyUberTaaController : ControllerBase
    {

        public StrategyUberTaaController()
        {
        }

        public class DailyData
        {
            public DateTime Date { get; set; }
            public double AdjClosePrice { get; set; }
        }

        [HttpGet]

        public ActionResult Index(int commo)
        {
            switch (commo)
                {
                    case 1: //GameChanger
                        return Content(GetStrGameChng(), "text/html");
                    case 2: //Global Asset
                        return Content(GetStrGlobalAssets(), "text/html");
                    default:
                        break;
                }
                return Content(GetStr2(), "text/html");
        }

        public string GetStr2()
        {
            return "Error";
        }
        // Under development - Daya
        public string GetStrGameChng()
        {
        //     // throw new NotImplementedException();
        //     //  Defining asset lists.
            // // string[] clmtAssetList = new string[]{ "^GSPC", "XLU", "VTI" };
            // string[] clmtAssetList = new string[]{ "^SPX", "XLU", "VTI" };    // Balazs: We can use SPY instead of ^GSPC
            // string[] gchAssetList = new string[]{ "AAPL", "ADBE", "AMZN", "CRM", "CRWD", "ETSY", "FB", "GOOGL", "MA", "MSFT", "NOW", "NVDA", "PYPL", "QCOM", "SE", "SHOP", "SQ", "V", "TLT"}; //TLT is used as a cashEquivalent
            // // string[] gchAssetList = new string[]{ "AAPL", "ADBE", "AMZN", "BABA", "CRM", "CRWD", "ETSY", "FB", "GOOGL", "ISRG", "MA", "MELI", "MSFT", "NFLX", "NOW", "NVDA", "PYPL", "QCOM", "ROKU", "SE", "SHOP", "SQ", "TDOC", "TWLO", "V", "ZM", "TLT"}; //TLT is used as a cashEquivalent
            // string[] gmrAssetList = new string[] { "MDY", "ILF", "FEZ", "EEM", "EPP", "VNQ", "TLT" }; //TLT is used as a cashEquivalent
            // string[] usedAssetList = Array.Empty<string>();
            // string titleString = "0";
            // string warningGCh ="";
            // switch (p_basketSelector)
            // {
            //     case 1:
            //         usedAssetList = gchAssetList;
            //         titleString = "GameChangers";
            //         warningGCh ="WARNING! Trading rules have been changed! Only live positions are valid, required trades are not!";
            //         break;
            //     case 2:
            //         usedAssetList = gmrAssetList;
            //         titleString = "Global Assets";
            //         break;
            // }

            // string[] allAssetList = new string[clmtAssetList.Length + usedAssetList.Length];
            // clmtAssetList.CopyTo(allAssetList, 0);
            // usedAssetList.CopyTo(allAssetList, clmtAssetList.Length);

            // string gchGSheetRef = "https://sheets.googleapis.com/v4/spreadsheets/1AGci_xFhgcC-Q1tEZ5E-HTBWbOU-C9ZXyjLIN1bEZeE/values/A1:AF2000?key=";
            // string gmrGSheetRef = "https://sheets.googleapis.com/v4/spreadsheets/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU/values/A1:Z2000?key=";
            // string gchGSheet2Ref = "https://docs.google.com/spreadsheets/d/1AGci_xFhgcC-Q1tEZ5E-HTBWbOU-C9ZXyjLIN1bEZeE/edit?usp=sharing";
            // string gmrGSheet2Ref = "https://docs.google.com/spreadsheets/d/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU/edit?usp=sharing";
            // string gchGDocRef = "https://docs.google.com/document/d/1JPyRJY7VrW7hQMagYLtB_ruTzEKEd8POHQy6sZ_Nnyk/edit?usp=sharing";
            // string gmrGDocRef = "https://docs.google.com/document/d/1-hDoFu1buI1XHvJZyt6Cq813Hw1TQWGl0jE7mwwS3l0/edit?usp=sharing";

            // string usedGSheetRef = (p_basketSelector == 1) ? gchGSheetRef : gmrGSheetRef;
            // string usedGSheet2Ref = (p_basketSelector == 1) ? gchGSheet2Ref : gmrGSheet2Ref;
            // string usedGDocRef = (p_basketSelector == 1) ? gchGDocRef : gmrGDocRef;

            // int thresholdLower = 25; //Upper threshold is 100-thresholdLower.
            // int[] lookbackDays = new int[] { 60, 120, 180, 252 };
            // int volDays = 20;

            //  //Collecting and splitting price data got from SQL Server
            // (IList<List<DailyData>>, List<List<DailyData>>, List<DailyData>) dataListTupleFromSQServer = GetStockHistData(allAssetList);

            // IList<List<DailyData>> quotesData = dataListTupleFromSQServer.Item1;
            // IList<List<DailyData>> quotesForClmtData = dataListTupleFromSQServer.Item2;
            // List<DailyData> cashEquivalentQuotesData = dataListTupleFromSQServer.Item3;

            // Debug.WriteLine("The Data from gSheet is :", quotesData, quotesForClmtData, cashEquivalentQuotesData);

            // // Calculating basic weights based on percentile channels - base Varadi TAA
            // Tuple<double[], double[,]> taaWeightResultsTuple = TaaWeights(quotesData, lookbackDays, volDays, thresholdLower);
            // Debug.WriteLine("The Data from gSheet is :", taaWeightResultsTuple);
            //Calculating CLMT data
            // var clmtRes = CLMTCalc(quotesForClmtData);

            // //Setting last data date
            // double lastDataDate = (clmtRes[0][^1] == taaWeightResultsTuple.Item1[^1]) ? clmtRes[0][^1] : 0;

            // //Get, split and convert GSheet data
            // var gSheetReadResult = UberTAAGChGoogleApiGsheet(usedGSheetRef);
            // string? content = ((ContentResult)gSheetReadResult).Content;
            // string? gSheetString = content;
            // Tuple<double[], int[,], int[], int[], string[], int[], int[]> gSheetResToFinCalc = GSheetConverter(gSheetString, allAssetList);
            // Debug.WriteLine("The Data from gSheet is :", gSheetResToFinCalc);

            // // Calculating final weights - Advanced UberTAA
            // Tuple<double[,], double[,], double[,], string[], string[]> weightsFinal = MultiplFinCalc(clmtRes, gSheetResToFinCalc, allAssetList, lastDataDate,taaWeightResultsTuple);

            // //Request time (UTC)
            // DateTime liveDateTime = DateTime.UtcNow;
            // string liveDate = liveDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            // DateTime timeNowET = Utils.ConvertTimeFromUtcToEt(liveDateTime);
            // string liveDateString = "Request time (UTC): " + liveDate;

            // //Last data time (UTC)
            // string lastDataTime = (quotesData[0][^1].Date.Date == liveDateTime.Date & timeNowET.TimeOfDay<=new DateTime(2000,1,1,16,15,0).TimeOfDay) ? "Live data at " + liveDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "Close price on "+ quotesData[0][^1].Date.ToString("yyyy-MM-dd");
            // string lastDataTimeString = "Last data time (UTC): "+lastDataTime;

            // //Current PV, Number of current and required shares
            // DateTime startMatlabDate = DateTime.ParseExact("1900/01/01", "yyyy/MM/dd", CultureInfo.InvariantCulture);
            // DateTime nextTradingDay = startMatlabDate.AddDays(weightsFinal.Item1[weightsFinal.Item1.GetLength(0) - 1, 0] - 693962);
            // string nextTradingDayString = nextTradingDay.ToString("yyyy-MM-dd");
            // DateTime currPosDate = startMatlabDate.AddDays(gSheetResToFinCalc.Item6[0] - 693962);
            // string currPosDateString = currPosDate.ToString("yyyy-MM-dd");

            // double currPV;
            // int[] currPosInt = new int[usedAssetList.Length + 1];
            

            // double[] currPosValue = new double[usedAssetList.Length+1];
            // for (int jCols = 0; jCols < currPosValue.Length-2; jCols++)
            // {
            //     currPosInt[jCols] = gSheetResToFinCalc.Item7[jCols];
            //     currPosValue[jCols] =quotesData[jCols][quotesData[0].Count-1].AdjClosePrice*currPosInt[jCols];
            // }
            // currPosInt[^2] = gSheetResToFinCalc.Item7[^1];
            // currPosInt[^1] = gSheetResToFinCalc.Item6[1];
            // currPosValue[^2] = cashEquivalentQuotesData[quotesData[0].Count - 1].AdjClosePrice * gSheetResToFinCalc.Item7[^1];
            // currPosValue[^1] = gSheetResToFinCalc.Item6[1];
            // currPV = Math.Round(currPosValue.Sum());

            // double[] nextPosValue = new double[usedAssetList.Length+1];
            // for (int jCols = 0; jCols < nextPosValue.Length - 2; jCols++)
            // {
            //     nextPosValue[jCols] = currPV*weightsFinal.Item3[weightsFinal.Item3.GetLength(0)-1,jCols+1];
            // }
            // nextPosValue[^2] = Math.Max(0,currPV- nextPosValue.Take(nextPosValue.Length - 2).ToArray().Sum());
            // nextPosValue[^1] = currPV - nextPosValue.Take(nextPosValue.Length - 1).ToArray().Sum();

            // double[] nextPosInt = new double[nextPosValue.Length];
            // for (int jCols = 0; jCols < nextPosInt.Length - 2; jCols++)
            // {
            //     nextPosInt[jCols] = nextPosValue[jCols]/ quotesData[jCols][quotesData[0].Count - 1].AdjClosePrice;
            // }
            // nextPosInt[^2] = nextPosValue[nextPosInt.Length - 2]/cashEquivalentQuotesData[quotesData[0].Count - 1].AdjClosePrice;
            // nextPosInt[^1] = nextPosValue[nextPosInt.Length - 1];

            // double[] posValueDiff = new double[usedAssetList.Length + 1];
            // for (int jCols = 0; jCols < posValueDiff.Length; jCols++)
            // {
            //     posValueDiff[jCols] = nextPosValue[jCols] - currPosValue[jCols];
            // }

            // double[] posIntDiff = new double[usedAssetList.Length + 1];
            // for (int jCols = 0; jCols < posIntDiff.Length; jCols++)
            // {
            //     posIntDiff[jCols] = nextPosInt[jCols] - currPosInt[jCols];
            // }

            // //CLMT
            // string clmtSignal;
            // if (clmtRes[7][^1]==1)
            // {
            //     clmtSignal = "bullish";
            // }
            // else if (clmtRes[7][^1] == 3)
            // {
            //     clmtSignal = "bearish";
            // }
            // else
            // {
            //     clmtSignal = "neutral";
            // }

            // string xluVtiSignal;
            // if (clmtRes[3][^1] == 1)
            // {
            //     xluVtiSignal = "bullish";
            // }
            // else 
            // {
            //     xluVtiSignal = "bearish";
            // }

            // string spxMASignal;
            // if (clmtRes[6][^1] == 1)
            // {
            //     spxMASignal = "bullish";
            // }
            // else
            // {
            //     spxMASignal = "bearish";
            // }


            // //Position weights in the last 20 days
            // string[,] prevPosMtx = new string[weightsFinal.Item3.GetLength(0)+1,usedAssetList.Length+3];
            // for (int iRows = 0; iRows < prevPosMtx.GetLength(0) - 1; iRows++)
            // {
            //     DateTime assDate = startMatlabDate.AddDays(weightsFinal.Item3[iRows, 0] - 693962);
            //     string assDateString = assDate.ToString("yyyy-MM-dd");
            //     prevPosMtx[iRows, 0] =assDateString;

            //     double assetWeightSum = 0;
            //     for (int jCols = 0; jCols < prevPosMtx.GetLength(1) - 4; jCols++)
            //     {
            //         assetWeightSum += weightsFinal.Item3[iRows, jCols + 1];
            //         prevPosMtx[iRows, jCols + 1] =Math.Round(weightsFinal.Item3[iRows,jCols+1]*100.0,2).ToString()+"%";
            //     }
            //     prevPosMtx[iRows, prevPosMtx.GetLength(1) - 1] = (weightsFinal.Item4[iRows]=="0")?"---":weightsFinal.Item4[iRows];
            //     prevPosMtx[iRows, prevPosMtx.GetLength(1)-3] = Math.Round(Math.Max((1.0-assetWeightSum),0)* 100.0, 2).ToString() + "%";
            //     prevPosMtx[iRows, prevPosMtx.GetLength(1)-2] = Math.Round((1.0 - assetWeightSum- Math.Max((1.0 - assetWeightSum), 0)) * 100.0, 2).ToString() + "%";
            // }
            // prevPosMtx[prevPosMtx.GetLength(0)-1, 0] = "";
            // for (int jCols = 0; jCols < prevPosMtx.GetLength(1) - 3; jCols++)
            // {
            //     prevPosMtx[prevPosMtx.GetLength(0) - 1, jCols+1]=usedAssetList[jCols];
            // }
            // prevPosMtx[prevPosMtx.GetLength(0) - 1, prevPosMtx.GetLength(1) - 2] = "Cash";
            // prevPosMtx[prevPosMtx.GetLength(0) - 1, prevPosMtx.GetLength(1) - 1] = "Event";

            // for (int iRows = 0; iRows < prevPosMtx.GetLength(0) / 2; iRows++)
            // {
            //     for (int jCols = 0; jCols < prevPosMtx.GetLength(1); jCols++)
            //     {
            //         string tmp = prevPosMtx[iRows, jCols];
            //         prevPosMtx[iRows, jCols] = prevPosMtx[prevPosMtx.GetLength(0) - iRows - 1, jCols];
            //         prevPosMtx[prevPosMtx.GetLength(0) - iRows - 1, jCols] = tmp;
            //     }
            // }

            // //Codes for last 20 days to coloring 
            // double[,] prevAssEventCodes = weightsFinal.Item1;
            // for (int iRows = 0; iRows < prevAssEventCodes.GetLength(0) / 2; iRows++)
            // {
            //     for (int jCols = 0; jCols < prevAssEventCodes.GetLength(1); jCols++)
            //     {
            //         double tmp = prevAssEventCodes[iRows, jCols];
            //         prevAssEventCodes[iRows, jCols] = prevAssEventCodes[prevAssEventCodes.GetLength(0) - iRows - 1, jCols];
            //         prevAssEventCodes[prevAssEventCodes.GetLength(0) - iRows - 1, jCols] = tmp;
            //     }
            // }

            // //Color codes for last 20 days
            // string[,] prevAssEventColorMtx = new string[weightsFinal.Item3.GetLength(0) + 1, usedAssetList.Length + 3];
            // for (int iRows = 0; iRows < prevAssEventColorMtx.GetLength(0)-1; iRows++)
            // {
            //     prevAssEventColorMtx[0, 0] = "66CCFF";
            //     prevAssEventColorMtx[0, prevAssEventColorMtx.GetLength(1) - 3] = "66CCFF";
            //     prevAssEventColorMtx[0, prevAssEventColorMtx.GetLength(1) - 2] = "66CCFF";
            //     prevAssEventColorMtx[0, prevAssEventColorMtx.GetLength(1) - 1] = "66CCFF";
            //     prevAssEventColorMtx[iRows + 1, 0] = "FF6633";
            //     prevAssEventColorMtx[iRows + 1, prevAssEventColorMtx.GetLength(1)-3] = "FFE4C4";
            //     prevAssEventColorMtx[iRows + 1, prevAssEventColorMtx.GetLength(1)-2] = "FFE4C4";
            //     prevAssEventColorMtx[iRows + 1, prevAssEventColorMtx.GetLength(1)-1] = "FFFF00";
            //     for (int jCols = 0; jCols < prevAssEventColorMtx.GetLength(1) - 4; jCols++)
            //     {
            //         prevAssEventColorMtx[0, jCols + 1] = "66CCFF";
            //         if (prevAssEventCodes[iRows, jCols+1] == 1)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "228B22";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 2)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "FF0000";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 3)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "7CFC00";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 4)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "DC143C";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 5)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "1E90FF";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 6)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "7B68EE";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 7)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "FFFFFF";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 8)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "00FFFF";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 9)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "A9A9A9";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 10)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "FF8C00";
            //         }
            //         else if (prevAssEventCodes[iRows, jCols+1] == 11)
            //         {
            //             prevAssEventColorMtx[iRows + 1, jCols + 1] = "F0E68C";
            //         }
            //     }
            // }


            // //Events in the next 10 days
            // string[,] futPosMtx = new string[weightsFinal.Item2.GetLength(0) + 1, usedAssetList.Length + 1];
            // string[,] futAssEventCodes = new string[weightsFinal.Item2.GetLength(0) + 1, usedAssetList.Length + 1];
            // for (int iRows = 0; iRows < futPosMtx.GetLength(0) - 1; iRows++)
            // {
            //     DateTime assFDate = startMatlabDate.AddDays(weightsFinal.Item2[iRows, 0] - 693962);
            //     string assFDateString = System.String.Empty;
            //     assFDateString = assFDate.ToString("yyyy-MM-dd");
            //     futPosMtx[iRows+1, 0] = assFDateString;
            //     futAssEventCodes[iRows + 1, 0] = "FF6633";

            //     for (int jCols = 0; jCols < futPosMtx.GetLength(1) - 2; jCols++)
            //     {
            //         if (weightsFinal.Item2[iRows, jCols + 1] == 1)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "FOMC Bullish Day";
            //             futAssEventCodes[iRows + 1, jCols+1] = "228B22";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 2)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "FOMC Bearish Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "FF0000";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 3)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "Holiday Bullish Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "7CFC00";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 4)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "Holiday Bearish Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "DC143C";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 5)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "Important Earnings Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "1E90FF";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 6)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "Pre-Earnings Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "7B68EE";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 7)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "Skipped Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "FFFFFF";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 8)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "CLMT Bullish Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "00FFFF";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 9)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "CLMT Neutral Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "A9A9A9";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 10)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "CLMT Bearish Day";
            //             futAssEventCodes[iRows + 1, jCols + 1] = "FF8C00";
            //         }
            //         else if (weightsFinal.Item2[iRows, jCols + 1] == 11)
            //         {
            //             futPosMtx[iRows + 1, jCols + 1] = "---"; //Unknown CLMT Day
            //             futAssEventCodes[iRows + 1, jCols + 1] = "F0E68C";
            //         }
            //     }

            //     futPosMtx[iRows + 1, futPosMtx.GetLength(1)-1] =  (weightsFinal.Item5[iRows]=="0")?"---": weightsFinal.Item5[iRows];
            //     futAssEventCodes[iRows + 1, futPosMtx.GetLength(1) - 1] = "FFFF00";
            // }
            // futPosMtx[0, 0] = "";
            // futAssEventCodes[0,0] = "66CCFF";
            // for (int jCols = 0; jCols < futPosMtx.GetLength(1) - 2; jCols++)
            // {
            //     futPosMtx[0, jCols + 1] = usedAssetList[jCols];
            //     futAssEventCodes[0, jCols + 1] = "66CCFF";
            // }

            // futPosMtx[0, futPosMtx.GetLength(1) - 1] = "Event";
            // futAssEventCodes[0, futPosMtx.GetLength(1) - 1] = "66CCFF";


            // //AssetPrice Changes in last 20 days to chart
            // int assetChartLength = 20;
            // string[,] assetChangesMtx = new string[assetChartLength+1,usedAssetList.Length];
            // for (int iRows = 0; iRows < assetChangesMtx.GetLength(0); iRows++)
            // {
            //     assetChangesMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            //     for (int jCols = 0; jCols < assetChangesMtx.GetLength(1)-1; jCols++)
            //     {
            //         assetChangesMtx[iRows, jCols+1] = Math.Round((quotesData[jCols][quotesData[jCols].Count-1-assetChartLength+iRows].AdjClosePrice/quotesData[jCols][quotesData[jCols].Count-1-assetChartLength].AdjClosePrice-1) * 100.0,2).ToString()+"%";
            //     }
            // }

            // //Daily changes, currently does not used.
            // string[,] assetDailyChangesMtx = new string[assetChartLength + 1, usedAssetList.Length];
            // for (int iRows = 0; iRows < assetDailyChangesMtx.GetLength(0); iRows++)
            // {
            //     assetDailyChangesMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            //     for (int jCols = 0; jCols < assetDailyChangesMtx.GetLength(1) - 1; jCols++)
            //     {
            //         assetDailyChangesMtx[iRows, jCols + 1] = Math.Round((quotesData[jCols][quotesData[jCols].Count - 1 - assetChartLength + iRows].AdjClosePrice / quotesData[jCols][quotesData[jCols].Count - 1 - assetChartLength + iRows - 1].AdjClosePrice - 1) * 100.0, 2).ToString() + "%";
            //     }
            // }

            // //Data for SPX MA chart
            // string[,] spxToChartMtx = new string[assetChartLength + 1, 4];
            // for (int iRows = 0; iRows < spxToChartMtx.GetLength(0); iRows++)
            // {
            //     spxToChartMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            //     spxToChartMtx[iRows, 1] = Math.Round(clmtRes[8][clmtRes[8].GetLength(0)-assetChartLength-1+iRows],0).ToString();
            //     spxToChartMtx[iRows, 2] = Math.Round(clmtRes[4][clmtRes[4].GetLength(0)-assetChartLength-1+iRows],0).ToString();
            //     spxToChartMtx[iRows, 3] = Math.Round(clmtRes[5][clmtRes[5].GetLength(0)-assetChartLength-1+iRows],0).ToString();
            // }

            // //Data for XLU-VTi RSI chart
            // string[,] xluVtiToChartMtx = new string[assetChartLength + 1, 3];
            // for (int iRows = 0; iRows < spxToChartMtx.GetLength(0); iRows++)
            // {
            //     xluVtiToChartMtx[iRows, 0] = quotesData[0][quotesData[0].Count - 1 - assetChartLength + iRows].Date.ToString("yyyy-MM-dd");
            //     xluVtiToChartMtx[iRows, 1] = Math.Round(clmtRes[1][clmtRes[1].GetLength(0) - assetChartLength - 1 + iRows], 0).ToString();
            //     xluVtiToChartMtx[iRows, 2] = Math.Round(clmtRes[2][clmtRes[2].GetLength(0) - assetChartLength - 1 + iRows], 0).ToString();
            // }


            // //Creating input string for JavaScript.
            // StringBuilder sb = new("{" + Environment.NewLine);
            // sb.Append(@"""titleCont"": """ + titleString);
            // sb.Append(@"""," + Environment.NewLine + @"""warningCont"": """ + warningGCh);
            // sb.Append(@"""," + Environment.NewLine + @"""requestTime"": """ + liveDateString);
            // sb.Append(@"""," + Environment.NewLine + @"""lastDataTime"": """ + lastDataTimeString);
            // sb.Append(@"""," + Environment.NewLine + @"""currentPV"": """ + currPV.ToString("#,##0"));
            // sb.Append(@"""," + Environment.NewLine + @"""currentPVDate"": """ + currPosDateString);
            // sb.Append(@"""," + Environment.NewLine + @"""clmtSign"": """ + clmtSignal);
            // sb.Append(@"""," + Environment.NewLine + @"""xluVtiSign"": """ + xluVtiSignal);
            // sb.Append(@"""," + Environment.NewLine + @"""spxMASign"": """ + spxMASignal);
            // sb.Append(@"""," + Environment.NewLine + @"""gDocRef"": """ + usedGDocRef);
            // sb.Append(@"""," + Environment.NewLine + @"""gSheetRef"": """ + usedGSheet2Ref);

            // sb.Append(@"""," + Environment.NewLine + @"""assetNames"": """);
            //     for (int i=0; i<usedAssetList.Length-1; i++)
            //         sb.Append(usedAssetList[i] + ", ");
            // sb.Append(usedAssetList[^1]);

            // sb.Append(@"""," + Environment.NewLine + @"""assetNames2"": """);
            // for (int i = 0; i < usedAssetList.Length; i++)
            //     sb.Append(usedAssetList[i] + ", ");
            // sb.Append("Cash");

            // sb.Append(@"""," + Environment.NewLine + @"""currPosNum"": """);
            // for (int i = 0; i < currPosInt.Length - 1; i++)
            //     sb.Append(currPosInt[i].ToString() + ", ");
            // sb.Append("$"+Math.Round(currPosInt[^1]/1000.0).ToString()+"K");

            // sb.Append(@"""," + Environment.NewLine + @"""currPosVal"": """);
            // for (int i = 0; i < currPosValue.Length - 1; i++)
            //     sb.Append("$"+Math.Round(currPosValue[i]/1000).ToString() + "K, ");
            // sb.Append("$"+Math.Round(currPosValue[^1]/1000).ToString()+"K");

            // sb.Append(@"""," + Environment.NewLine + @"""nextPosNum"": """);
            // for (int i = 0; i < nextPosInt.Length - 1; i++)
            //     sb.Append(Math.Round(nextPosInt[i]).ToString() + ", ");
            // sb.Append("$"+Math.Round(nextPosInt[^1]/1000).ToString()+"K");

            // sb.Append(@"""," + Environment.NewLine + @"""nextPosVal"": """);
            // for (int i = 0; i < nextPosValue.Length - 1; i++)
            //     sb.Append("$"+Math.Round(nextPosValue[i]/1000).ToString() + "K, ");
            // sb.Append("$"+Math.Round(nextPosValue[^1]/1000).ToString()+"K");

            // sb.Append(@"""," + Environment.NewLine + @"""posNumDiff"": """);
            // for (int i = 0; i < posIntDiff.Length - 1; i++)
            //     sb.Append(Math.Round(posIntDiff[i]).ToString() + ", ");
            // sb.Append("$" + Math.Round(posIntDiff[^1] / 1000).ToString() + "K");

            // sb.Append(@"""," + Environment.NewLine + @"""posValDiff"": """);
            // for (int i = 0; i < posValueDiff.Length - 1; i++)
            //     sb.Append("$" + Math.Round(posValueDiff[i] / 1000).ToString() + "K, ");
            // sb.Append("$" + Math.Round(posValueDiff[^1] / 1000).ToString() + "K");

            // sb.Append(@"""," + Environment.NewLine + @"""nextTradingDay"": """ + nextTradingDayString);
            // sb.Append(@"""," + Environment.NewLine + @"""currPosDate"": """ + currPosDateString);

            // sb.Append(@"""," + Environment.NewLine + @"""prevPositionsMtx"": """);
            // for (int i = 0; i < prevPosMtx.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < prevPosMtx.GetLength(1)-1; j++)
            //     {
            //         sb.Append(prevPosMtx[i, j] + ", ");
            //     }
            //     sb.Append(prevPosMtx[i, prevPosMtx.GetLength(1)-1]);
            //     if (i < prevPosMtx.GetLength(0)-1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }

            // sb.Append(@"""," + Environment.NewLine + @"""prevAssEventMtx"": """);
            // for (int i = 0; i < prevAssEventColorMtx.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < prevAssEventColorMtx.GetLength(1) - 1; j++)
            //     {
            //         sb.Append(prevAssEventColorMtx[i, j] + ",");
            //     }
            //     sb.Append(prevAssEventColorMtx[i, prevAssEventColorMtx.GetLength(1) - 1]);
            //     if (i < prevAssEventColorMtx.GetLength(0) - 1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }


            // sb.Append(@"""," + Environment.NewLine + @"""futPositionsMtx"": """);
            // for (int i = 0; i < futPosMtx.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < futPosMtx.GetLength(1) - 1; j++)
            //     {
            //         sb.Append(futPosMtx[i, j] + ", ");
            //     }
            //     sb.Append(futPosMtx[i, futPosMtx.GetLength(1) - 1]);
            //     if (i < futPosMtx.GetLength(0) - 1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }

            // sb.Append(@"""," + Environment.NewLine + @"""futAssEventMtx"": """);
            // for (int i = 0; i < futAssEventCodes.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < futAssEventCodes.GetLength(1) - 1; j++)
            //     {
            //         sb.Append(futAssEventCodes[i, j] + ",");
            //     }
            //     sb.Append(futAssEventCodes[i, futAssEventCodes.GetLength(1) - 1]);
            //     if (i < futAssEventCodes.GetLength(0) - 1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }

            // sb.Append(@"""," + Environment.NewLine + @"""chartLength"": """ + assetChartLength); 


            // sb.Append(@"""," + Environment.NewLine + @"""assetChangesToChartMtx"": """);
            // for (int i = 0; i < assetChangesMtx.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < assetChangesMtx.GetLength(1) - 1; j++)
            //     {
            //         sb.Append(assetChangesMtx[i, j] + ", ");
            //     }
            //     sb.Append(assetChangesMtx[i, assetChangesMtx.GetLength(1) - 1]);
            //     if (i < assetChangesMtx.GetLength(0) - 1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }

            // sb.Append(@"""," + Environment.NewLine + @"""assetDailyChangesToChartMtx"": """);
            // for (int i = 0; i < assetDailyChangesMtx.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < assetDailyChangesMtx.GetLength(1) - 1; j++)
            //     {
            //         sb.Append(assetDailyChangesMtx[i, j] + ", ");
            //     }
            //     sb.Append(assetDailyChangesMtx[i, assetDailyChangesMtx.GetLength(1) - 1]);
            //     if (i < assetDailyChangesMtx.GetLength(0) - 1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }

            // sb.Append(@"""," + Environment.NewLine + @"""spxMAToChartMtx"": """);
            // for (int i = 0; i < spxToChartMtx.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < spxToChartMtx.GetLength(1) - 1; j++)
            //     {
            //         sb.Append(spxToChartMtx[i, j] + ", ");
            //     }
            //     sb.Append(spxToChartMtx[i, spxToChartMtx.GetLength(1) - 1]);
            //     if (i < spxToChartMtx.GetLength(0) - 1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }

            // sb.Append(@"""," + Environment.NewLine + @"""xluVtiPercToChartMtx"": """);
            // for (int i = 0; i < xluVtiToChartMtx.GetLength(0); i++)
            // {
            //     sb.Append("");
            //     for (int j = 0; j < xluVtiToChartMtx.GetLength(1) - 1; j++)
            //     {
            //         sb.Append(xluVtiToChartMtx[i, j] + ", ");
            //     }
            //     sb.Append(xluVtiToChartMtx[i, xluVtiToChartMtx.GetLength(1) - 1]);
            //     if (i < xluVtiToChartMtx.GetLength(0) - 1)
            //     {
            //         sb.Append("ß ");
            //     }
            // }


            // sb.AppendLine(@"""" + Environment.NewLine + @"}");

            // //var asdfa = sb.ToString(); //testing created string to JS

            // return sb.ToString();
            

            Thread.Sleep(1000);     // intentional delay to simulate a longer process to crunch data. This can be removed.
            string mockupTestResponse = @"{
                ""titleCont"": ""Game Changer"",
                ""warningCont"": """",
                ""requestTime"": ""Request time (UTC): 2022-03-30 11:14:23"",
                ""lastDataTime"": ""Last data time (UTC): Close price on 2022-03-29"",
                ""currentPV"": ""516,395"",
                ""currentPVDate"": ""2022-02-15"",
                ""clmtSign"": ""bearish"",
                ""xluVtiSign"": ""bearish"",
                ""spxMASign"": ""bearish"",
                ""gDocRef"": ""https://docs.google.com/document/d/1-hDoFu1buI1XHvJZyt6Cq813Hw1TQWGl0jE7mwwS3l0/edit?usp=sharing"",
                ""gSheetRef"": ""https://docs.google.com/spreadsheets/d/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU/edit?usp=sharing"",
                ""assetNames"": ""MDY, ILF, FEZ, EEM, EPP, VNQ, TLT"",
                ""assetNames2"": ""MDY, ILF, FEZ, EEM, EPP, VNQ, TLT, Cash"",
                ""currPosNum"": ""0, 0, 0, 0, 0, 0, 3950, $0K"",
                ""currPosVal"": ""$0K, $0K, $0K, $0K, $0K, $0K, $516K, $0K"",
                ""nextPosNum"": ""0, 2370, 0, 0, 852, 935, 2285, $0K"",
                ""nextPosVal"": ""$0K, $72K, $0K, $0K, $43K, $103K, $299K, $0K"",
                ""posNumDiff"": ""0, 2370, 0, 0, 852, 935, -1665, $0K"",
                ""posValDiff"": ""$0K, $72K, $0K, $0K, $43K, $103K, $-218K, $0K"",
                ""nextTradingDay"": ""2022-03-30"",
                ""currPosDate"": ""2022-02-15"",
                ""prevPositionsMtx"": "", MDY, ILF, FEZ, EEM, EPP, VNQ, TLT, Cash, Eventß 2022-03-30, 0%, 13.91%, 0%, 0%, 8.24%, 20%, 57.85%, 0%, ---ß 2022-03-29, 0%, 21.6%, 0%, 0%, 0%, 0%, 78.4%, 0%, ---ß 2022-03-28, 0%, 14.95%, 0%, 0%, 0%, 0%, 85.05%, 0%, ---ß 2022-03-25, 0%, 15.29%, 0%, 0%, 0%, 0%, 84.71%, 0%, ---ß 2022-03-24, 0%, 15.01%, 0%, 0%, 0%, 0%, 84.99%, 0%, ---ß 2022-03-23, 0%, 14.97%, 0%, 0%, 0%, 0%, 85.03%, 0%, ---ß 2022-03-22, 0%, 15.03%, 0%, 0%, 0%, 0%, 84.97%, 0%, ---ß 2022-03-21, 0%, 15.25%, 0%, 0%, 0%, 0%, 84.75%, 0%, ---ß 2022-03-18, 0%, 13.1%, 0%, 0%, 0%, 0%, 86.9%, 0%, ---ß 2022-03-17, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, FOMC+1ß 2022-03-16, 0%, 31.23%, 0%, 0%, 0%, 0%, 68.77%, 0%, FOMC0ß 2022-03-15, 0%, 31.37%, 0%, 0%, 0%, 0%, 68.63%, 0%, FOMC-1ß 2022-03-14, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, FOMC-2ß 2022-03-11, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, FOMC-3ß 2022-03-10, 0%, 34.56%, 0%, 0%, 0%, 0%, 65.44%, 0%, FOMC-4ß 2022-03-09, 0%, 19.51%, 0%, 0%, 0%, 0%, 80.49%, 0%, ---ß 2022-03-08, 0%, 19.31%, 0%, 0%, 0%, 0%, 80.69%, 0%, ---ß 2022-03-07, 0%, 21.09%, 0%, 0%, 0%, 0%, 78.91%, 0%, ---ß 2022-03-04, 0%, 20.09%, 0%, 0%, 0%, 0%, 79.91%, 0%, ---ß 2022-03-03, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, ---"",
                ""prevAssEventMtx"": ""66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFFß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF0000,FF0000,FF0000,FF0000,FF0000,FF0000,FFE4C4,FFE4C4,FFFF00ß FF6633,228B22,228B22,228B22,228B22,228B22,228B22,FFE4C4,FFE4C4,FFFF00ß FF6633,228B22,228B22,228B22,228B22,228B22,228B22,FFE4C4,FFE4C4,FFFF00ß FF6633,FF0000,FF0000,FF0000,FF0000,FF0000,FF0000,FFE4C4,FFE4C4,FFFF00ß FF6633,FF0000,FF0000,FF0000,FF0000,FF0000,FF0000,FFE4C4,FFE4C4,FFFF00ß FF6633,228B22,228B22,228B22,228B22,228B22,228B22,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00"",
                ""futPositionsMtx"": "", MDY, ILF, FEZ, EEM, EPP, VNQ, Eventß 2022-03-31, ---, ---, ---, ---, ---, ---, ---ß 2022-04-01, ---, ---, ---, ---, ---, ---, ---ß 2022-04-04, ---, ---, ---, ---, ---, ---, ---ß 2022-04-05, ---, ---, ---, ---, ---, ---, ---ß 2022-04-06, ---, ---, ---, ---, ---, ---, ---ß 2022-04-07, ---, ---, ---, ---, ---, ---, ---ß 2022-04-08, ---, ---, ---, ---, ---, ---, ---ß 2022-04-11, ---, ---, ---, ---, ---, ---, ---ß 2022-04-12, ---, ---, ---, ---, ---, ---, ---ß 2022-04-13, ---, ---, ---, ---, ---, ---, ---"",
                ""futAssEventMtx"": ""66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFFß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00"",
                ""chartLength"": ""20"",
                ""assetChangesToChartMtx"": ""2022-03-01, 0%, 0%, 0%, 0%, 0%, 0%ß 2022-03-02, 2.65%, 1.3%, 2.02%, 0.17%, 1.67%, 1.87%ß 2022-03-03, 1.78%, 3.12%, -0.87%, -1.24%, 0.72%, 2.71%ß 2022-03-04, 0.21%, 2.23%, -6.05%, -3.23%, 1.06%, 3.18%ß 2022-03-07, -3.56%, -1.19%, -9.55%, -6.85%, -1.11%, 1.14%ß 2022-03-08, -3.25%, -0.22%, -6.9%, -6.55%, -2.32%, 0.65%ß 2022-03-09, -0.5%, 2.41%, -0.37%, -3.9%, -0.41%, 2.2%ß 2022-03-10, -0.48%, 2.15%, -3.35%, -5.7%, 0.35%, 2.45%ß 2022-03-11, -1.51%, 0.63%, -4.17%, -7.68%, -0.48%, 1.64%ß 2022-03-14, -2.33%, -1.71%, -2.12%, -9.91%, -1.5%, 0.84%ß 2022-03-15, -0.87%, -2.9%, -1.17%, -9.78%, -1.11%, 1.52%ß 2022-03-16, 1.95%, -0.48%, 3.55%, -2.52%, 3.3%, 2.73%ß 2022-03-17, 2.94%, 2.19%, 3.67%, -3.01%, 4.06%, 4.31%ß 2022-03-18, 3.45%, 4.53%, 4.02%, -1.63%, 5.95%, 4.31%ß 2022-03-21, 3.17%, 6.76%, 2.35%, -3.14%, 5.47%, 3.68%ß 2022-03-22, 3.81%, 7.91%, 3.85%, -1.39%, 6.45%, 3.94%ß 2022-03-23, 1.88%, 9.06%, 1.45%, -2.04%, 6%, 2.22%ß 2022-03-24, 3.06%, 10.95%, 2.35%, -1.52%, 7.39%, 2.86%ß 2022-03-25, 3.79%, 11.66%, 2.45%, -2.3%, 7.47%, 4.15%ß 2022-03-28, 3.94%, 10.95%, 3.25%, -1.97%, 7.1%, 5.25%ß 2022-03-29, 6.16%, 12.55%, 6.65%, -0.3%, 8.52%, 8.34%"",
                ""assetDailyChangesToChartMtx"": ""2022-03-01, -1.91%, 0.11%, -4.12%, -1.33%, -1.31%, -0.53%ß 2022-03-02, 2.65%, 1.3%, 2.02%, 0.17%, 1.67%, 1.87%ß 2022-03-03, -0.85%, 1.8%, -2.84%, -1.41%, -0.94%, 0.82%ß 2022-03-04, -1.54%, -0.86%, -5.22%, -2.02%, 0.35%, 0.46%ß 2022-03-07, -3.76%, -3.34%, -3.72%, -3.74%, -2.15%, -1.98%ß 2022-03-08, 0.32%, 0.98%, 2.93%, 0.33%, -1.23%, -0.48%ß 2022-03-09, 2.85%, 2.64%, 7%, 2.83%, 1.96%, 1.54%ß 2022-03-10, 0.02%, -0.25%, -2.98%, -1.87%, 0.76%, 0.25%ß 2022-03-11, -1.04%, -1.49%, -0.85%, -2.09%, -0.82%, -0.79%ß 2022-03-14, -0.83%, -2.32%, 2.14%, -2.42%, -1.03%, -0.78%ß 2022-03-15, 1.5%, -1.21%, 0.97%, 0.14%, 0.4%, 0.67%ß 2022-03-16, 2.84%, 2.49%, 4.78%, 8.05%, 4.46%, 1.19%ß 2022-03-17, 0.97%, 2.69%, 0.12%, -0.51%, 0.74%, 1.55%ß 2022-03-18, 0.49%, 2.29%, 0.34%, 1.43%, 1.82%, 0%ß 2022-03-21, -0.27%, 2.13%, -1.61%, -1.54%, -0.45%, -0.61%ß 2022-03-22, 0.62%, 1.08%, 1.46%, 1.81%, 0.93%, 0.26%ß 2022-03-23, -1.86%, 1.07%, -2.31%, -0.66%, -0.43%, -1.66%ß 2022-03-24, 1.15%, 1.74%, 0.89%, 0.53%, 1.31%, 0.63%ß 2022-03-25, 0.71%, 0.64%, 0.1%, -0.79%, 0.08%, 1.25%ß 2022-03-28, 0.15%, -0.63%, 0.78%, 0.33%, -0.34%, 1.06%ß 2022-03-29, 2.14%, 1.44%, 3.29%, 1.7%, 1.32%, 2.93%"",
                ""spxMAToChartMtx"": ""2022-03-01, 4306, 4545, 4463ß 2022-03-02, 4387, 4540, 4464ß 2022-03-03, 4363, 4536, 4465ß 2022-03-04, 4329, 4530, 4466ß 2022-03-07, 4201, 4520, 4466ß 2022-03-08, 4171, 4509, 4466ß 2022-03-09, 4278, 4498, 4467ß 2022-03-10, 4260, 4488, 4467ß 2022-03-11, 4204, 4476, 4467ß 2022-03-14, 4173, 4464, 4467ß 2022-03-15, 4262, 4454, 4467ß 2022-03-16, 4358, 4445, 4468ß 2022-03-17, 4412, 4437, 4469ß 2022-03-18, 4463, 4433, 4470ß 2022-03-21, 4461, 4428, 4472ß 2022-03-22, 4512, 4425, 4473ß 2022-03-23, 4456, 4420, 4474ß 2022-03-24, 4520, 4417, 4476ß 2022-03-25, 4543, 4413, 4477ß 2022-03-28, 4541, 4411, 4479ß 2022-03-29, 4632, 4410, 4481"",
                ""xluVtiPercToChartMtx"": ""2022-03-01, 41, 42ß 2022-03-02, 49, 44ß 2022-03-03, 50, 41ß 2022-03-04, 58, 44ß 2022-03-07, 63, 38ß 2022-03-08, 59, 37ß 2022-03-09, 56, 41ß 2022-03-10, 57, 37ß 2022-03-11, 64, 37ß 2022-03-14, 63, 39ß 2022-03-15, 68, 44ß 2022-03-16, 69, 45ß 2022-03-17, 70, 47ß 2022-03-18, 66, 53ß 2022-03-21, 67, 55ß 2022-03-22, 68, 59ß 2022-03-23, 74, 59ß 2022-03-24, 74, 59ß 2022-03-25, 72, 56ß 2022-03-28, 72, 57ß 2022-03-29, 78, 63""
                }";
            return mockupTestResponse;
        }
        public string GetStrGlobalAssets()
        {
            Thread.Sleep(1000);     // intentional delay to simulate a longer process to crunch data. This can be removed.
            string mockupTestResponse = @"{
                ""titleCont"": ""Global Assets"",
                ""warningCont"": """",
                ""requestTime"": ""Request time (UTC): 2022-03-30 11:14:23"",
                ""lastDataTime"": ""Last data time (UTC): Close price on 2022-03-29"",
                ""currentPV"": ""516,395"",
                ""currentPVDate"": ""2022-02-15"",
                ""clmtSign"": ""bearish"",
                ""xluVtiSign"": ""bearish"",
                ""spxMASign"": ""bearish"",
                ""gDocRef"": ""https://docs.google.com/document/d/1-hDoFu1buI1XHvJZyt6Cq813Hw1TQWGl0jE7mwwS3l0/edit?usp=sharing"",
                ""gSheetRef"": ""https://docs.google.com/spreadsheets/d/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU/edit?usp=sharing"",
                ""assetNames"": ""MDY, ILF, FEZ, EEM, EPP, VNQ, TLT"",
                ""assetNames2"": ""MDY, ILF, FEZ, EEM, EPP, VNQ, TLT, Cash"",
                ""currPosNum"": ""0, 0, 0, 0, 0, 0, 3950, $0K"",
                ""currPosVal"": ""$0K, $0K, $0K, $0K, $0K, $0K, $516K, $0K"",
                ""nextPosNum"": ""0, 2370, 0, 0, 852, 935, 2285, $0K"",
                ""nextPosVal"": ""$0K, $72K, $0K, $0K, $43K, $103K, $299K, $0K"",
                ""posNumDiff"": ""0, 2370, 0, 0, 852, 935, -1665, $0K"",
                ""posValDiff"": ""$0K, $72K, $0K, $0K, $43K, $103K, $-218K, $0K"",
                ""nextTradingDay"": ""2022-03-30"",
                ""currPosDate"": ""2022-02-15"",
                ""prevPositionsMtx"": "", MDY, ILF, FEZ, EEM, EPP, VNQ, TLT, Cash, Eventß 2022-03-30, 0%, 13.91%, 0%, 0%, 8.24%, 20%, 57.85%, 0%, ---ß 2022-03-29, 0%, 21.6%, 0%, 0%, 0%, 0%, 78.4%, 0%, ---ß 2022-03-28, 0%, 14.95%, 0%, 0%, 0%, 0%, 85.05%, 0%, ---ß 2022-03-25, 0%, 15.29%, 0%, 0%, 0%, 0%, 84.71%, 0%, ---ß 2022-03-24, 0%, 15.01%, 0%, 0%, 0%, 0%, 84.99%, 0%, ---ß 2022-03-23, 0%, 14.97%, 0%, 0%, 0%, 0%, 85.03%, 0%, ---ß 2022-03-22, 0%, 15.03%, 0%, 0%, 0%, 0%, 84.97%, 0%, ---ß 2022-03-21, 0%, 15.25%, 0%, 0%, 0%, 0%, 84.75%, 0%, ---ß 2022-03-18, 0%, 13.1%, 0%, 0%, 0%, 0%, 86.9%, 0%, ---ß 2022-03-17, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, FOMC+1ß 2022-03-16, 0%, 31.23%, 0%, 0%, 0%, 0%, 68.77%, 0%, FOMC0ß 2022-03-15, 0%, 31.37%, 0%, 0%, 0%, 0%, 68.63%, 0%, FOMC-1ß 2022-03-14, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, FOMC-2ß 2022-03-11, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, FOMC-3ß 2022-03-10, 0%, 34.56%, 0%, 0%, 0%, 0%, 65.44%, 0%, FOMC-4ß 2022-03-09, 0%, 19.51%, 0%, 0%, 0%, 0%, 80.49%, 0%, ---ß 2022-03-08, 0%, 19.31%, 0%, 0%, 0%, 0%, 80.69%, 0%, ---ß 2022-03-07, 0%, 21.09%, 0%, 0%, 0%, 0%, 78.91%, 0%, ---ß 2022-03-04, 0%, 20.09%, 0%, 0%, 0%, 0%, 79.91%, 0%, ---ß 2022-03-03, 0%, 0%, 0%, 0%, 0%, 0%, 100%, 0%, ---"",
                ""prevAssEventMtx"": ""66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFFß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FF8C00,FFE4C4,FFE4C4,FFFF00ß FF6633,FF0000,FF0000,FF0000,FF0000,FF0000,FF0000,FFE4C4,FFE4C4,FFFF00ß FF6633,228B22,228B22,228B22,228B22,228B22,228B22,FFE4C4,FFE4C4,FFFF00ß FF6633,228B22,228B22,228B22,228B22,228B22,228B22,FFE4C4,FFE4C4,FFFF00ß FF6633,FF0000,FF0000,FF0000,FF0000,FF0000,FF0000,FFE4C4,FFE4C4,FFFF00ß FF6633,FF0000,FF0000,FF0000,FF0000,FF0000,FF0000,FFE4C4,FFE4C4,FFFF00ß FF6633,228B22,228B22,228B22,228B22,228B22,228B22,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00ß FF6633,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,A9A9A9,FFE4C4,FFE4C4,FFFF00"",
                ""futPositionsMtx"": "", MDY, ILF, FEZ, EEM, EPP, VNQ, Eventß 2022-03-31, ---, ---, ---, ---, ---, ---, ---ß 2022-04-01, ---, ---, ---, ---, ---, ---, ---ß 2022-04-04, ---, ---, ---, ---, ---, ---, ---ß 2022-04-05, ---, ---, ---, ---, ---, ---, ---ß 2022-04-06, ---, ---, ---, ---, ---, ---, ---ß 2022-04-07, ---, ---, ---, ---, ---, ---, ---ß 2022-04-08, ---, ---, ---, ---, ---, ---, ---ß 2022-04-11, ---, ---, ---, ---, ---, ---, ---ß 2022-04-12, ---, ---, ---, ---, ---, ---, ---ß 2022-04-13, ---, ---, ---, ---, ---, ---, ---"",
                ""futAssEventMtx"": ""66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFF,66CCFFß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00ß FF6633,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,F0E68C,FFFF00"",
                ""chartLength"": ""20"",
                ""assetChangesToChartMtx"": ""2022-03-01, 0%, 0%, 0%, 0%, 0%, 0%ß 2022-03-02, 2.65%, 1.3%, 2.02%, 0.17%, 1.67%, 1.87%ß 2022-03-03, 1.78%, 3.12%, -0.87%, -1.24%, 0.72%, 2.71%ß 2022-03-04, 0.21%, 2.23%, -6.05%, -3.23%, 1.06%, 3.18%ß 2022-03-07, -3.56%, -1.19%, -9.55%, -6.85%, -1.11%, 1.14%ß 2022-03-08, -3.25%, -0.22%, -6.9%, -6.55%, -2.32%, 0.65%ß 2022-03-09, -0.5%, 2.41%, -0.37%, -3.9%, -0.41%, 2.2%ß 2022-03-10, -0.48%, 2.15%, -3.35%, -5.7%, 0.35%, 2.45%ß 2022-03-11, -1.51%, 0.63%, -4.17%, -7.68%, -0.48%, 1.64%ß 2022-03-14, -2.33%, -1.71%, -2.12%, -9.91%, -1.5%, 0.84%ß 2022-03-15, -0.87%, -2.9%, -1.17%, -9.78%, -1.11%, 1.52%ß 2022-03-16, 1.95%, -0.48%, 3.55%, -2.52%, 3.3%, 2.73%ß 2022-03-17, 2.94%, 2.19%, 3.67%, -3.01%, 4.06%, 4.31%ß 2022-03-18, 3.45%, 4.53%, 4.02%, -1.63%, 5.95%, 4.31%ß 2022-03-21, 3.17%, 6.76%, 2.35%, -3.14%, 5.47%, 3.68%ß 2022-03-22, 3.81%, 7.91%, 3.85%, -1.39%, 6.45%, 3.94%ß 2022-03-23, 1.88%, 9.06%, 1.45%, -2.04%, 6%, 2.22%ß 2022-03-24, 3.06%, 10.95%, 2.35%, -1.52%, 7.39%, 2.86%ß 2022-03-25, 3.79%, 11.66%, 2.45%, -2.3%, 7.47%, 4.15%ß 2022-03-28, 3.94%, 10.95%, 3.25%, -1.97%, 7.1%, 5.25%ß 2022-03-29, 6.16%, 12.55%, 6.65%, -0.3%, 8.52%, 8.34%"",
                ""assetDailyChangesToChartMtx"": ""2022-03-01, -1.91%, 0.11%, -4.12%, -1.33%, -1.31%, -0.53%ß 2022-03-02, 2.65%, 1.3%, 2.02%, 0.17%, 1.67%, 1.87%ß 2022-03-03, -0.85%, 1.8%, -2.84%, -1.41%, -0.94%, 0.82%ß 2022-03-04, -1.54%, -0.86%, -5.22%, -2.02%, 0.35%, 0.46%ß 2022-03-07, -3.76%, -3.34%, -3.72%, -3.74%, -2.15%, -1.98%ß 2022-03-08, 0.32%, 0.98%, 2.93%, 0.33%, -1.23%, -0.48%ß 2022-03-09, 2.85%, 2.64%, 7%, 2.83%, 1.96%, 1.54%ß 2022-03-10, 0.02%, -0.25%, -2.98%, -1.87%, 0.76%, 0.25%ß 2022-03-11, -1.04%, -1.49%, -0.85%, -2.09%, -0.82%, -0.79%ß 2022-03-14, -0.83%, -2.32%, 2.14%, -2.42%, -1.03%, -0.78%ß 2022-03-15, 1.5%, -1.21%, 0.97%, 0.14%, 0.4%, 0.67%ß 2022-03-16, 2.84%, 2.49%, 4.78%, 8.05%, 4.46%, 1.19%ß 2022-03-17, 0.97%, 2.69%, 0.12%, -0.51%, 0.74%, 1.55%ß 2022-03-18, 0.49%, 2.29%, 0.34%, 1.43%, 1.82%, 0%ß 2022-03-21, -0.27%, 2.13%, -1.61%, -1.54%, -0.45%, -0.61%ß 2022-03-22, 0.62%, 1.08%, 1.46%, 1.81%, 0.93%, 0.26%ß 2022-03-23, -1.86%, 1.07%, -2.31%, -0.66%, -0.43%, -1.66%ß 2022-03-24, 1.15%, 1.74%, 0.89%, 0.53%, 1.31%, 0.63%ß 2022-03-25, 0.71%, 0.64%, 0.1%, -0.79%, 0.08%, 1.25%ß 2022-03-28, 0.15%, -0.63%, 0.78%, 0.33%, -0.34%, 1.06%ß 2022-03-29, 2.14%, 1.44%, 3.29%, 1.7%, 1.32%, 2.93%"",
                ""spxMAToChartMtx"": ""2022-03-01, 4306, 4545, 4463ß 2022-03-02, 4387, 4540, 4464ß 2022-03-03, 4363, 4536, 4465ß 2022-03-04, 4329, 4530, 4466ß 2022-03-07, 4201, 4520, 4466ß 2022-03-08, 4171, 4509, 4466ß 2022-03-09, 4278, 4498, 4467ß 2022-03-10, 4260, 4488, 4467ß 2022-03-11, 4204, 4476, 4467ß 2022-03-14, 4173, 4464, 4467ß 2022-03-15, 4262, 4454, 4467ß 2022-03-16, 4358, 4445, 4468ß 2022-03-17, 4412, 4437, 4469ß 2022-03-18, 4463, 4433, 4470ß 2022-03-21, 4461, 4428, 4472ß 2022-03-22, 4512, 4425, 4473ß 2022-03-23, 4456, 4420, 4474ß 2022-03-24, 4520, 4417, 4476ß 2022-03-25, 4543, 4413, 4477ß 2022-03-28, 4541, 4411, 4479ß 2022-03-29, 4632, 4410, 4481"",
                ""xluVtiPercToChartMtx"": ""2022-03-01, 41, 42ß 2022-03-02, 49, 44ß 2022-03-03, 50, 41ß 2022-03-04, 58, 44ß 2022-03-07, 63, 38ß 2022-03-08, 59, 37ß 2022-03-09, 56, 41ß 2022-03-10, 57, 37ß 2022-03-11, 64, 37ß 2022-03-14, 63, 39ß 2022-03-15, 68, 44ß 2022-03-16, 69, 45ß 2022-03-17, 70, 47ß 2022-03-18, 66, 53ß 2022-03-21, 67, 55ß 2022-03-22, 68, 59ß 2022-03-23, 74, 59ß 2022-03-24, 74, 59ß 2022-03-25, 72, 56ß 2022-03-28, 72, 57ß 2022-03-29, 78, 63""
                }";
            return mockupTestResponse;
        }
        public object UberTAAGChGoogleApiGsheet(string p_usedGSheetRef)
        {
            Utils.Logger.Info("UberTAAGChGoogleApiGsheet() BEGIN");

            string? valuesFromGSheetStr = "Error. Make sure GoogleApiKeyKey, GoogleApiKeyKey is in SQLab.WebServer.SQLab.NoGitHub.json !";
            if (!String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) && !String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
            {
                valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync(p_usedGSheetRef + Utils.Configuration["Google:GoogleApiKeyKey"], 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
                if (valuesFromGSheetStr == null)
                    valuesFromGSheetStr = "Error in DownloadStringWithRetry().";
            }
            
            Utils.Logger.Info("UberTAAGChGoogleApiGsheet() END");
            return Content($"<HTML><body>UberTAAGChGoogleApiGsheet() finished OK. <br> Received data: '{valuesFromGSheetStr}'</body></HTML>", "text/html");
        }
        public static Tuple< double[], int[,], int[], int[], string[], int[], int[]> GSheetConverter(string? p_gSheetString, string[] p_allAssetList)
        {
            if (p_gSheetString != null)
            {
                string[] gSheetTableRows = p_gSheetString.Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);
                string currPosRaw = gSheetTableRows[3];
                currPosRaw = currPosRaw.Replace("\n", "").Replace("]", "").Replace("\",", "BRB").Replace("\"", "").Replace(" ", "").Replace(",", "");
                string[] currPos = currPosRaw.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);
                string[] currPosAP = new string[p_allAssetList.Length - 3];
                Array.Copy(currPos, 2, currPosAP, 0, p_allAssetList.Length - 3);
                int currPosDate = Int32.Parse(currPos[0]);
                int currPosCash = Int32.Parse(currPos[^3]);
                int[] currPosDateCash = new int[] {currPosDate,currPosCash };
                int[] currPosAssets = Array.ConvertAll(currPosAP, int.Parse);
                            

                p_gSheetString = p_gSheetString.Replace("\n", "").Replace("]", "").Replace("\"", "").Replace(" ", "").Replace(",,", ",0,");
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
             DateTime startIncLoc = nowET.AddYears(-1).AddDays(-160);

            List<List<DailyData>> quotesData = new();
            List<List<DailyData>> quotesForClmtData = new();
            List<DailyData> cashEquivalentQuotesData = new();

            List<(Asset asset, List<AssetHistValue> values)> assetHistsAndEst = MemDb.gMemDb.GetSdaHistClosesAndLastEstValue(assets, startIncLoc).ToList();
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
            var cashVals = assetHistsAndEst[^1].values;
            for (int j = 0; j < cashVals.Count; j++)
                cashEquivalentQuotesData.Add(new DailyData() { Date = cashVals[j].Date, AdjClosePrice = cashVals[j].SdaValue });

            return (quotesData, quotesForClmtData, cashEquivalentQuotesData);
        }

        // public static Tuple<double[], double[,]> TaaWeights(IList<List<DailyData>> p_taaWeightsData, int[] p_pctChannelLookbackDays, int p_histVolLookbackDays, int p_thresholdLower)
        // {
        //     var dshd = p_taaWeightsData;
        //     int nAssets = p_taaWeightsData.Count;

        //     double[] assetScores = new double[nAssets];
        //     double[] assetScoresMod = new double[nAssets];
        //     double[] assetHV = new double[nAssets];
        //     double[] assetWeights = new double[nAssets];
        //     double[] assetWeights2 = new double[nAssets];
        //     double[,] assetPctChannelsUpper = new double[nAssets, p_pctChannelLookbackDays.Length];  // for assets and for each 
        //     double[,] assetPctChannelsLower = new double[nAssets, p_pctChannelLookbackDays.Length];  // for assets and for each
        //     sbyte[,] assetPctChannelsSignal = new sbyte[nAssets, p_pctChannelLookbackDays.Length];  // for assets and for each
        //     int startNumDay = p_pctChannelLookbackDays.Max()-1;
        //     double thresholdLower = p_thresholdLower / 100.0;
        //     double thresholdUpper = 1-thresholdLower;

        //     int nDays = p_taaWeightsData[0].Count - startNumDay;
        //     double[,] dailyAssetWeights = new double[nDays,nAssets];
        //     double[,] dailyAssetScores = new double[nDays, nAssets];
        //     double[,] dailyAssetScoresMod = new double[nDays, nAssets];
        //     double[,] dailyAssetHv = new double[nDays, nAssets];
        //     for (int iDay = 0; iDay < nDays; iDay++)
        //     {
        //         for (int iAsset = 0; iAsset < nAssets; iAsset++)
        //         {
        //             double assetPrice = p_taaWeightsData[iAsset][startNumDay + iDay].AdjClosePrice;
        //             for (int iChannel = 0; iChannel < p_pctChannelLookbackDays.Length; iChannel++)
        //             {
        //                 // A long position would be initiated if the price exceeds the 75th percentile of prices over the last “n” days.The position would be closed if the price falls below the 25th percentile of prices over the last “n” days.
        //                 var usedQuotes = p_taaWeightsData[iAsset].GetRange(startNumDay + iDay - (p_pctChannelLookbackDays[iChannel] - 1), p_pctChannelLookbackDays[iChannel]).Select(r => r.AdjClosePrice);
        //                 assetPctChannelsLower[iAsset, iChannel] = Utils.Quantile(usedQuotes, thresholdLower);
        //                 assetPctChannelsUpper[iAsset, iChannel] = Utils.Quantile(usedQuotes, thresholdUpper);
        //                 if (assetPrice < assetPctChannelsLower[iAsset, iChannel])
        //                 assetPctChannelsSignal[iAsset, iChannel] = -1;
        //                 else if (assetPrice > assetPctChannelsUpper[iAsset, iChannel])
        //                 assetPctChannelsSignal[iAsset, iChannel] = 1;
        //                 else if (iDay==0)
        //                 assetPctChannelsSignal[iAsset, iChannel] = 1;
        //             }
        //         }

        //         // Calculate assetWeights
        //         double totalWeight = 0.0;
                
        //         for (int iAsset = 0; iAsset < nAssets; iAsset++)
        //         {
        //             sbyte compositeSignal = 0;    // For every stocks, sum up the four signals every day. This sum will be -4, -2, 0, +2 or +4.
        //             for (int iChannel = 0; iChannel < p_pctChannelLookbackDays.Length; iChannel++)
        //             {
        //                 compositeSignal += assetPctChannelsSignal[iAsset, iChannel];
        //             }
        //             assetScores[iAsset] = compositeSignal / 4.0;    // Divide it by 4 to get a signal between -1 and +1 (this will be the “score”).
        //             assetScoresMod[iAsset] = compositeSignal / 8.0 + 0.5;    // Divide it by 4 to get a signal between -1 and +1 (this will be the “score”).

        //             double[] hvPctChg = new double[p_histVolLookbackDays];
        //             for (int iHv = 0; iHv < p_histVolLookbackDays; iHv++)
        //             {
        //                 hvPctChg[p_histVolLookbackDays - iHv - 1] = p_taaWeightsData[iAsset][startNumDay + iDay - iHv].AdjClosePrice / p_taaWeightsData[iAsset][startNumDay + iDay - iHv - 1].AdjClosePrice - 1;
        //             }
        //             // Balazs: uses "corrected sample standard deviation"; corrected: dividing by 19, not 20; He doesn't annualize. He uses daily StDev
        //             assetHV[iAsset] = Utils.StandardDeviation(hvPctChg);  // Calculate the 20-day historical volatility of daily percentage changes for every stock.
        //             assetWeights[iAsset] = assetScores[iAsset] / assetHV[iAsset];   // “Score/Vol” quotients will define the weights of the stocks. They can be 0 or negative as well. 
        //                                                                             // there is an interesting observation here. Actually, it is a good behavour.
        //                                                                             // If assetScores[i]=0, assetWeights[i] becomes 0, so we don't use its weight when p_isCashAllocatedForNonActives => TLT will not fill its Cash-place; NO TLT will be invested (if this is the only stock with 0 score), the portfolio will be 100% in other stocks. We are more Brave.
        //                                                                             // However, if assetScores[i]<0 (negative), assetWeights[i] becoumes a proper negative number. It will be used in TotalWeight calculation => TLT will fill its's space. (if this is the only stock with negative score), TLT will be invested in its place; consequently the portfolio will NOT be 100% in other stocks. We are more defensive.
        //             totalWeight += Math.Abs(assetWeights[iAsset]);      // Sum up the absolute values of the “Score/Vol” quotients. TotalWeight contains even the non-active assets so have have some cash.
        //             assetWeights2[iAsset] = (assetWeights[iAsset]>=0) ?assetWeights[iAsset]:0.0;

        //         }
        //         for (int iAsset = 0; iAsset < nAssets; iAsset++)
        //         {
        //             dailyAssetWeights[iDay, iAsset] = assetWeights2[iAsset]/totalWeight;
        //             dailyAssetScores[iDay, iAsset] = assetScores[iAsset];
        //             dailyAssetHv[iDay, iAsset] = assetHV[iAsset];
        //             dailyAssetScoresMod[iDay, iAsset] = assetScoresMod[iAsset];
        //         }

        //     }

        //     IEnumerable<DateTime> taaWeightDateVec = p_taaWeightsData[0].GetRange(p_taaWeightsData[0].Count-nDays ,nDays).Select(r => r.Date);
        //     DateTime[] taaWeightDateArray = taaWeightDateVec.ToArray();
        //     DateTime startMatlabDate = DateTime.ParseExact("1900/01/01", "yyyy/MM/dd", CultureInfo.InvariantCulture);

        //     double[] taaWeightMatlabDateVec = new double[taaWeightDateVec.Count()];
        //     for (int i = 0; i < taaWeightMatlabDateVec.Length; i++)
        //     {
        //         taaWeightMatlabDateVec[i] = (taaWeightDateArray[i] - startMatlabDate).TotalDays + 693962;
        //     }

        //     Tuple<double[],double[,]> taaWeightResults = Tuple.Create(taaWeightMatlabDateVec, dailyAssetWeights);
        //     //Tuple<double[],double[,]> taaWeightResults = Tuple.Create(taaWeightMatlabDateVec, dailyAssetScoresMod);
        //     return taaWeightResults;
        // }

        // public static double[][] CLMTCalc(IList<List<DailyData>> p_quotesForClmtData)
        // {
        //     double[,] p_clmtData = new double[p_quotesForClmtData[0].Count, 4];

        //     IEnumerable<DateTime> clmtDateVec = p_quotesForClmtData[0].Select(r => r.Date);
        //     DateTime[] clmtDateArray = clmtDateVec.ToArray();
        //     DateTime startMatlabDate = DateTime.ParseExact("1900/01/01", "yyyy/MM/dd", CultureInfo.InvariantCulture);

        //     double[] clmtMatlabDateVec = new double[clmtDateVec.Count()];
        //     for (int i = 0; i < clmtMatlabDateVec.Length; i++)
        //     {
        //         clmtMatlabDateVec[i] = (clmtDateArray[i] - startMatlabDate).TotalDays + 693962;
        //     }

        //     for (int iRows = 0; iRows < p_clmtData.GetLength(0); iRows++)
        //     {
        //         p_clmtData[iRows, 0] = clmtMatlabDateVec[iRows];
        //         for (int jCols = 0; jCols < p_clmtData.GetLength(1)-1; jCols++)
        //         {
        //             p_clmtData[iRows,jCols+1]=p_quotesForClmtData[jCols][iRows].AdjClosePrice;
        //         }
        //     }


        //     double[] xluRSI =new double[p_clmtData.GetLength(0)-200];
        //     for (int iRows = 0; iRows < xluRSI.Length; iRows++)
        //     {
        //         double losses = new();
        //         double gains = new();
        //         int lossNum = 0;
        //         int gainNum = 0;
        //         for (int kRows = 0; kRows < 20; kRows++)
        //         {
        //             if (p_clmtData[iRows + kRows + 181, 2] - p_clmtData[iRows + kRows+180, 2] >= 0)
        //             {
        //                 gains = gains + p_clmtData[iRows + kRows + 181, 2] - p_clmtData[iRows + kRows+180, 2];
        //                 gainNum += 1; 
        //             }
        //             else
        //             {
        //                 losses = losses + p_clmtData[iRows + kRows + 181, 2] - p_clmtData[iRows + kRows+180, 2];
        //                 lossNum += 1;
        //             }
        //         }
        //         xluRSI[iRows] = 100 - 100 * (-losses / (-losses + gains));

        //     }

        //     double[] vtiRSI = new double[p_clmtData.GetLength(0) - 200];
        //     for (int iRows = 0; iRows < vtiRSI.Length; iRows++)
        //     {
        //         double losses = new();
        //         double gains = new();
        //         for (int kRows = 0; kRows < 20; kRows++)
        //         {
        //             if (p_clmtData[iRows + kRows + 181, 3] - p_clmtData[iRows + kRows+180, 3] >= 0)
        //             {
        //                 gains = gains + p_clmtData[iRows + kRows + 181, 3] - p_clmtData[iRows + kRows+180, 3];
        //             }
        //             else
        //             {
        //                 losses = losses + p_clmtData[iRows + kRows + 181, 3] - p_clmtData[iRows + kRows+180, 3];
        //             }
        //         }
        //         vtiRSI[iRows] = 100 - 100 * (-losses / (-losses + gains));

        //     }

        //     double[] xluVtiIndi = new double[xluRSI.Length];
        //     for (int iRows = 0; iRows < xluVtiIndi.Length; iRows++)
        //     {
        //         xluVtiIndi[iRows] = (xluRSI[iRows]>=vtiRSI[iRows]) ?2:1;
        //     }

        //     double[] spxMA50 = new double[p_clmtData.GetLength(0) - 200];
        //     double[] spxPrice = new double[p_clmtData.GetLength(0) - 200];
        //     for (int iRows = 0; iRows < spxMA50.Length; iRows++)
        //     {
        //         spxPrice[iRows] = p_clmtData[iRows+200,1];
        //         double sumsSPX50 = new();
                
        //         for (int kRows = 0; kRows < 50; kRows++)
        //         {
        //             sumsSPX50 += p_clmtData[iRows + kRows+151,1];
        //         }
        //         spxMA50[iRows] = sumsSPX50 / 50;

        //     }

        //     double[] spxMA200 = new double[p_clmtData.GetLength(0) - 200];
        //     for (int iRows = 0; iRows < spxMA200.Length; iRows++)
        //     {
        //         double sumsSPX200 = new();

        //         for (int kRows = 0; kRows < 200; kRows++)
        //         {
        //             sumsSPX200 += p_clmtData[iRows + kRows+1, 1];
        //         }
        //         spxMA200[iRows] = sumsSPX200 / 200;

        //     }

        //     double[] spxMAIndi = new double[spxMA50.Length];
        //     for (int iRows = 0; iRows < spxMAIndi.Length; iRows++)
        //     {
        //         spxMAIndi[iRows] = (spxMA50[iRows] >= spxMA200[iRows]) ? 1 : 0;
        //     }

        //     double[] clmtIndi = new double[spxMAIndi.Length];
        //     for (int iRows = 0; iRows < clmtIndi.Length; iRows++)
        //     {
        //         if (spxMAIndi[iRows]==1 & xluVtiIndi[iRows]==1)
        //         {
        //             clmtIndi[iRows] = 1;
        //         }
        //         else if (spxMAIndi[iRows] == 0 & xluVtiIndi[iRows] == 2)
        //         {
        //             clmtIndi[iRows] = 3;
        //         }
        //         else
        //         {
        //             clmtIndi[iRows] = 2;
        //         }
        //     }

        //     double[] clmtDateVec2 = new double[clmtIndi.Length];
        //     for (int iRows = 0; iRows < clmtDateVec2.Length; iRows++)
        //     {
        //         clmtDateVec2[iRows] = p_clmtData[iRows+200,0];
        //     }
            
        //     double[][] clmtTotalResu = new double[9][];
        //     clmtTotalResu[0] = clmtDateVec2;
        //     clmtTotalResu[1] = xluRSI;
        //     clmtTotalResu[2] = vtiRSI;
        //     clmtTotalResu[3] = xluVtiIndi;
        //     clmtTotalResu[4] = spxMA50;
        //     clmtTotalResu[5] = spxMA200;
        //     clmtTotalResu[6] = spxMAIndi;
        //     clmtTotalResu[7] = clmtIndi;
        //     clmtTotalResu[8] = spxPrice;

        // //     StringBuilder stringBuilder=new StringBuilder();
        // //     foreach (var item in clmtTotalResu)
        // //     {
        // //         foreach (var item2 in item)
        // //         {
        // //             stringBuilder.Append(item2 + ",");
        // //         }
        // //         stringBuilder.AppendLine("ß" + Environment.NewLine + Environment.NewLine);
        // //     }

        // //     System.IO.File.WriteAllText(@"D:\xxx.csv", stringBuilder.ToString());

        //     return clmtTotalResu;
        // }

        // public Tuple<double[,], double[,], double[,], string[], string[]> MultiplFinCalc(double[][] p_clmtRes, Tuple<double[], int[,], int[], int[], string[], int[], int[]>  p_gSheetResToFinCalc, string[] p_allAssetList, double p_lastDataDate, Tuple<double[], double[,]>  p_taaWeightResultsTuple)
        // {

        //     int pastDataLength = 20;
        //     int futDataLength = 10;
        //     int indClmtRes = Array.IndexOf(p_clmtRes[0], p_lastDataDate);
        //     int indGSheetRes = Array.IndexOf(p_gSheetResToFinCalc.Item1, p_lastDataDate);
        //     int indWeightsRes = Array.IndexOf(p_taaWeightResultsTuple.Item1, p_lastDataDate);

        //     double[,] pastCodes = new double[pastDataLength ,p_allAssetList.Length - 3];
        //     double[,] futCodes = new double[futDataLength, p_allAssetList.Length - 3];
        //     string[] pastEvents = new string[pastDataLength];
        //     string[] futEvents = new string[futDataLength];


        //     for (int iRows = 0; iRows < pastCodes.GetLength(0); iRows++)
        //     {
        //         pastEvents[iRows] = p_gSheetResToFinCalc.Item5[indGSheetRes - pastDataLength + iRows + 2];
        //         pastCodes[iRows, 0] = p_gSheetResToFinCalc.Item1[indGSheetRes - pastDataLength + iRows + 2];
        //         for (int jCols = 1; jCols < pastCodes.GetLength(1); jCols++)
        //         {
        //             if (p_gSheetResToFinCalc.Item2[indGSheetRes - pastDataLength + iRows+2, jCols - 1] == 9)
        //             {
        //                 pastCodes[iRows, jCols] = 7;
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows+2] == 1)
        //             {
        //                 pastCodes[iRows, jCols] = 1;
        //             }
        //             else if (p_gSheetResToFinCalc.Item2[indGSheetRes - pastDataLength + iRows+2, jCols - 1] == 3)
        //             {
        //                 pastCodes[iRows, jCols] = 5;
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows+2] == 2)
        //             {
        //                 pastCodes[iRows, jCols] = 2;
        //             }
        //             else if (p_gSheetResToFinCalc.Item2[indGSheetRes - pastDataLength + iRows+2, jCols - 1] == 1)
        //             {
        //                 if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows+2] == 3)
        //                 {
        //                     pastCodes[iRows, jCols] = 3;
        //                 }
        //                 else
        //                 {
        //                     pastCodes[iRows, jCols] = 6;
        //                 }
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows+2] == 3)
        //             {
        //                 pastCodes[iRows, jCols] = 3;
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes - pastDataLength + iRows+2] == 4)
        //             {
        //                 pastCodes[iRows, jCols] = 4;
        //             }
        //             else if (p_clmtRes[7][indClmtRes - pastDataLength + iRows+1]==1)
        //             {
        //                 pastCodes[iRows, jCols] = 8;
        //             }
        //             else if (p_clmtRes[7][indClmtRes - pastDataLength + iRows+1] == 2)
        //             {
        //                 pastCodes[iRows, jCols] = 9;
        //             }
        //             else if (p_clmtRes[7][indClmtRes - pastDataLength + iRows+1] == 3)
        //             {
        //                 pastCodes[iRows, jCols] = 10;
        //             }

        //         }
        //     }

        //     for (int iRows = 0; iRows < futCodes.GetLength(0); iRows++)
        //     {
        //         futEvents[iRows] = p_gSheetResToFinCalc.Item5[indGSheetRes + iRows + 2];
        //         futCodes[iRows, 0] = p_gSheetResToFinCalc.Item1[indGSheetRes + iRows + 2];
        //         for (int jCols = 1; jCols < futCodes.GetLength(1); jCols++)
        //         {
        //             if (p_gSheetResToFinCalc.Item2[indGSheetRes + iRows+2, jCols - 1] == 9)
        //             {
        //                 futCodes[iRows, jCols] = 7;
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows+2] == 1)
        //             {
        //                 futCodes[iRows, jCols] = 1;
        //             }
        //             else if (p_gSheetResToFinCalc.Item2[indGSheetRes + iRows+2, jCols - 1] == 3)
        //             {
        //                 futCodes[iRows, jCols] = 5;
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows+2] == 2)
        //             {
        //                 futCodes[iRows, jCols] = 2;
        //             }
        //             else if (p_gSheetResToFinCalc.Item2[indGSheetRes + iRows+2, jCols - 1] == 1)
        //             {
        //                 if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows+2] == 3)
        //                 {
        //                     futCodes[iRows, jCols] = 3;
        //                 }
        //                 else
        //                 {
        //                     futCodes[iRows, jCols] = 6;
        //                 }
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows+2] == 3)
        //             {
        //                 futCodes[iRows, jCols] = 3;
        //             }
        //             else if (p_gSheetResToFinCalc.Item3[indGSheetRes + iRows+2] == 4)
        //             {
        //                 futCodes[iRows, jCols] = 4;
        //             }
        //             else
        //             {
        //                 futCodes[iRows, jCols] = 11;
        //             }

        //         }
        //     }

        //     double[,] pastWeightsFinal = new double[pastCodes.GetLength(0), p_allAssetList.Length - 3];
        //     double numAss = Convert.ToDouble(p_allAssetList.Length-4);
        //     for (int iRows = 0; iRows < pastWeightsFinal.GetLength(0); iRows++)
        //     {
        //         pastWeightsFinal[iRows, 0] = pastCodes[iRows, 0];
        //         for (int jCols = 1; jCols < pastWeightsFinal.GetLength(1); jCols++)
        //         {
        //             if (pastCodes[iRows, jCols] == 7)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 0;
        //             }
        //             else if (pastCodes[iRows, jCols] == 1)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 1.75*p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1,jCols-1];
        //             }
        //             else if (pastCodes[iRows, jCols] == 5)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = Math.Max(1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1], 1 / numAss);
        //             }
        //             else if (pastCodes[iRows, jCols] == 2)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 0;
        //             }
        //             else if (pastCodes[iRows, jCols] == 3)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
        //             }
        //             else if (pastCodes[iRows, jCols] == 6)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = Math.Max(1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1], 1 / numAss);
        //                 // pastWeightsFinal[iRows, jCols] = Math.Max(1.25 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1], 1 / numAss); #Mr.C. decided to increase leverage to 50% on bullish days
        //             }
        //             else if (pastCodes[iRows, jCols] == 4)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 0;
        //             }
        //             else if (pastCodes[iRows, jCols] == 8)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 1.5 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
        //                 // pastWeightsFinal[iRows, jCols] = 1.2 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1]; #Mr.C. decided to increase leverage to 50% on bullish days
        //             }
        //             else if (pastCodes[iRows, jCols] == 9)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 1 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
        //                 // pastWeightsFinal[iRows, jCols] = 0.8 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
        //             }
        //             else if (pastCodes[iRows, jCols] == 10)
        //             {
        //                 pastWeightsFinal[iRows, jCols] = 0.6 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
        //                 // pastWeightsFinal[iRows, jCols] = 0.4 * p_taaWeightResultsTuple.Item2[indWeightsRes - pastDataLength + iRows + 1, jCols - 1];
        //             }
        //         }
        //     }

        //             Tuple<double[,], double[,], double[,], string[], string[]> multiplFinResults = Tuple.Create(pastCodes, futCodes, pastWeightsFinal, pastEvents, futEvents);

        //     return multiplFinResults;
        // }
    }
}