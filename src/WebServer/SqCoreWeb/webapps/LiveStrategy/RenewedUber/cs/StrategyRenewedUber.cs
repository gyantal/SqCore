using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SqCommon;

namespace SqCoreWeb.Controllers
{    
    [ApiController]
    [Route("[controller]")]
    [ResponseCache(CacheProfileName = "NoCache")]
    public class StrategyRenewedUberController : ControllerBase
    {
        public class ExampleMessage
        {
            public string MsgType { get; set; } = string.Empty;

            public string StringData { get; set; } = string.Empty;
            public DateTime DateOrTime { get; set; }

            public int IntData { get; set; }

            public int IntDataFunction => 32 + (int)(IntData / 0.5556);
        }

        public StrategyRenewedUberController()
        {
        }

        // [HttpGet] // only 1 HttpGet attribute should be in the Controller (or you have to specify in it how to resolve)
        public IEnumerable<ExampleMessage> Get_old()
        {
            Thread.Sleep(1000);     // intentional delay to simulate a longer process to crunch data. This can be removed.

            var userEmailClaim = HttpContext?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            string email = userEmailClaim?.Value  ?? "Unknown email";

            var firstMsgToSend = new ExampleMessage
            {
                MsgType = "AdminMsg",
                StringData = $"Cookies says your email is '{email}'.",
                DateOrTime = DateTime.Now,
                IntData = 0,                
            };

            string[] RandomStringDataToSend = new[]  { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
            var rng = new Random();
            return (new ExampleMessage[] { firstMsgToSend }.Concat(Enumerable.Range(1, 5).Select(index => new ExampleMessage
            {
                MsgType = "Msg-type",
                StringData = RandomStringDataToSend[rng.Next(RandomStringDataToSend.Length)],
                DateOrTime = DateTime.Now.AddDays(index),
                IntData = rng.Next(-20, 55)                
            }))).ToArray();
        }

        [HttpGet]   // only 1 HttpGet attribute should be in the Controller (or you have to specify in it how to resolve)
        public string Get()
        {
            Thread.Sleep(1000);     // intentional delay to simulate a longer process to crunch data. This can be removed.
    // Under Development - Daya.
    //Defining asset lists.
            // //string[] allAssetList = new string[] { "VXX", "TQQQ", "UPRO", "SVXY", "TMV", "UCO", "UNG" };
            // //string[] allAssetListVIX = new string[] { "VXX", "TQQQ", "UPRO", "SVXY", "TMV", "UCO", "UNG", "^VIX" };
            // string[] allAssetList = new string[] { "VIXY", "TQQQ", "UPRO", "SVXY", "TMV", "UCO", "UNG" };
            // // string[] allAssetListVIX = new string[] { "VIXY", "TQQQ", "UPRO", "SVXY", "TMV", "UCO", "UNG", "^VIX" };

            // // double[] bullishWeights = new double[] { -0.1, 0.3, 0.3, 0.2, -0.1, -0.075, -0.15 };
            // // double[] bearishWeights = new double[] { 1, 0, 0, 0, 0, 0, 0 };
            // // double[] eventMultiplicator = new double[] { 0.5, 1, 1, 0.85, 0.85, 0.7, 0.7 };
            // // double[] stciThresholds = new double[] { 0.02, 0.09, 0.075 };


            // string gchGSheetRefPos = "https://sheets.googleapis.com/v4/spreadsheets/1OZV2MqNJAep9SV1p1YribbHYiYoI7Qz9OjQutV6qJt4/values/A1:Z2000?key=";
            // // string gchGSheet2RefPos = "https://docs.google.com/spreadsheets/d/1OZV2MqNJAep9SV1p1YribbHYiYoI7Qz9OjQutV6qJt4/edit?usp=sharing";
            // // string gchGSheetRef = "https://sheets.googleapis.com/v4/spreadsheets/1QjGsXw6YxPT0He5kE4YJ5o52ZCnX7cLA5N-V3Ng1juA/values/A1:Z2000?key=";
            // // string gchGSheet2Ref = "https://docs.google.com/spreadsheets/d/1QjGsXw6YxPT0He5kE4YJ5o52ZCnX7cLA5N-V3Ng1juA/edit#gid=0";
            // // string gchGDocRef = "https://docs.google.com/document/d/1q2nSfQUos93q4-dd0ILjTrlvtiQKnwlsg3zN1_0_lyI/edit?usp=sharing";
            // string usedGSheetRefPos = gchGSheetRefPos;
            // //Get, split and convert GSheet data
            // var gSheetReadResultPos = RenewedUberGoogleApiGsheet(usedGSheetRefPos);
            // string? content = ((ContentResult)gSheetReadResultPos).Content;
            // string? gSheetString = content;

            // Tuple<double[], DateTime[], string[,], int[], int[], int[], string[]> gSheetResToFinCalc = GSheetConverter(gSheetString, allAssetList);
            // // for debugging purpose - DAYA
            // Utils.Logger.Info(gSheetResToFinCalc);

            // multiline verbatim string literal format in C#
            string mockupTestResponse = @"{
""requestTime"": ""Request time (UTC): 2022-03-30 12:09:54"",
""lastDataTime"": ""Last data time (UTC): Live data at 2022-03-30 12:09:54"",
""currentPV"": ""1,919,059"",
""dailyProfSig"": ""+$"",
""dailyProfAbs"": ""10,850"",
""dailyProfString"": ""posDaily"",
""currentPVDate"": ""2022-03-30"",
""gDocRef"": ""https://docs.google.com/document/d/1q2nSfQUos93q4-dd0ILjTrlvtiQKnwlsg3zN1_0_lyI/edit?usp=sharing"",
""gSheetRef"": ""https://docs.google.com/spreadsheets/d/1OZV2MqNJAep9SV1p1YribbHYiYoI7Qz9OjQutV6qJt4/edit?usp=sharing"",
""currentEventSignal"": ""-1"",
""currentEventCode"": ""6"",
""currentEventCodeOriginal"": ""6"",
""currentEventName"": ""Other Bearish Event Day"",
""currentWeightedSignal"": ""-0.7"",
""currentSTCI"": ""8.6%"",
""currentVIX"": ""19.55"",
""currentFinalWeightMultiplier"": ""70%"",
""assetNames"": ""VXX, TQQQ, UPRO, SVXY, TMV, UCO, UNG"",
""assetNames2"": ""VXX, TQQQ, UPRO, SVXY, TMV, UCO, UNG, Cash"",
""currPosNum"": ""35000, 0, 0, 0, 0, 0, 0, $1038K"",
""currPosVal"": ""$881K, $0K, $0K, $0K, $0K, $0K, $0K, $1038K"",
""nextPosNum"": ""53392, 0, 0, 0, 0, 0, 0, $576K"",
""nextPosVal"": ""$1343K, $0K, $0K, $0K, $0K, $0K, $0K, $576K"",
""posNumDiff"": ""18392, 0, 0, 0, 0, 0, 0, $-463K"",
""posValDiff"": ""$463K, $0K, $0K, $0K, $0K, $0K, $0K, $-463K"",
""nextTradingDay"": ""2022-03-31"",
""currPosDate"": ""2022-03-30"",
""prevEventNames"": ""Other Bearish Event Day, Non-Playable Other Event Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day, FOMC Bearish Day"",
""prevEventColors"": ""ed8c8c, C0C0C0, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f, c24f4f"",
""nextEventColors"": ""00FA9A, FFFF00, FFFF00, FFFF00, FFFF00, FFFF00, FFFF00, FFFF00, FFFF00, 7CFC00, 7CFC00, 7CFC00, 7CFC00, 7CFC00, FFFF00"",
""pastDataMtxToJS"": ""2022-03-31, ToM-1, -1, ---, ---, ---, ---, ---, ---, Other Bearish Event Day, -1, -70%, 8.6%, 19.55, Other Bearish Event Day, -70%ß 2022-03-31, ToM-1, -1, ---, ---, ---, ---, ---, ---, Other Bearish Event Day, -1, -70%, 9.02%, 18.9, Non-Playable Other Event Day, 0%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 20.8, FOMC Bearish Day, -100%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 20.81, FOMC Bearish Day, -100%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 21.67, FOMC Bearish Day, -93.3%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 23.57, FOMC Bearish Day, -74.3%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 22.94, FOMC Bearish Day, -80.6%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 23.53, FOMC Bearish Day, -74.7%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 23.87, FOMC Bearish Day, -71.3%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 25.67, FOMC Bearish Day, -53.3%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 26.67, FOMC Bearish Day, -43.3%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 29.83, FOMC Bearish Day, -11.7%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 31.77, FOMC Bearish Day, -10%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 30.75, FOMC Bearish Day, -10%ß 2019-07-26, FOMC-3, -1, ToM-4, 1, ---, ---, ---, ---, FOMC Bearish Day, -1, -100%, 0%, 30.23, FOMC Bearish Day, -10%"",
""nextDataMtxToJS"": ""2022-04-01, ToM+1, +1, ---, ---, ---, ---, ---, ---, Other Bullish Event Day, 1, 70%ß 2022-04-04, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-05, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-06, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-07, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-08, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-11, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-12, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-13, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%ß 2022-04-14, GF-1, +1, ---, ---, ---, ---, ---, ---, Holiday Bullish Day, 1, 85%ß 2022-04-18, GF+1, +1, ---, ---, ---, ---, ---, ---, Holiday Bullish Day, 1, 85%ß 2022-04-19, GF+2, +1, OPEX+2, +1, ToMM+2, +1, VIXFUTEX-1, +1, Holiday Bullish Day, 1, 85%ß 2022-04-20, GF+3, +1, OPEX+3, +1, VIXFUTEX+1, +1, ---, ---, Holiday Bullish Day, 1, 85%ß 2022-04-21, GF+4, +1, OPEX+4, +1, ---, ---, ---, ---, Holiday Bullish Day, 1, 85%ß 2022-04-22, ---, ---, ---, ---, ---, ---, ---, ---, Non-Event Day, 0, 0%"",
""chartLength"": ""20"",
""assetChangesToChartMtx"": ""2022-03-02, 0%, 0%, 0%, 0%, 0%, 0%, 0%ß 2022-03-03, 0.6%, -4.18%, -1.46%, -0.28%, -3.21%, -0.62%, -2.23%ß 2022-03-04, 4.97%, -8.28%, -3.84%, -2.56%, -8.09%, 11.64%, 1.88%ß 2022-03-07, 14.14%, -18.52%, -12.39%, -6.03%, -6.11%, 17.71%, -0.41%ß 2022-03-08, 12.78%, -19.65%, -14.46%, -6.13%, -3.32%, 21.99%, -5.58%ß 2022-03-09, 8.29%, -11.04%, -7.52%, -4.31%, -0.05%, -3.57%, -6.52%ß 2022-03-10, 3.49%, -13.9%, -8.84%, -2.18%, 4.1%, -5.17%, -4.11%ß 2022-03-11, 5.53%, -19.24%, -12.25%, -3.19%, 2.88%, 0.41%, -1.35%ß 2022-03-14, 15.42%, -23.97%, -14.21%, -4.79%, 10.14%, -7.82%, -3.23%ß 2022-03-15, 14.98%, -16.94%, -8.54%, -3.15%, 10.59%, -15.91%, -4.76%ß 2022-03-16, 3.97%, -7.61%, -2.57%, 1.68%, 7.4%, -17.11%, -1.94%ß 2022-03-17, 4.77%, -4.52%, 1.08%, 2.52%, 9.98%, -5.58%, 2.06%ß 2022-03-18, 0.16%, 1.41%, 4.41%, 5.37%, 6%, -3.34%, 1.18%ß 2022-03-21, 4.21%, 0.69%, 4.41%, 5.47%, 13.5%, 7.07%, 2.47%ß 2022-03-22, 1.94%, 6.64%, 7.98%, 6.27%, 17.36%, 4.81%, 6.29%ß 2022-03-23, 2.12%, 2.14%, 3.81%, 6.45%, 9.65%, 11.72%, 5.93%ß 2022-03-24, 0.96%, 8.75%, 8.41%, 7.77%, 12.51%, 6.15%, 11.46%ß 2022-03-25, 0.96%, 8.58%, 10.04%, 9.29%, 17.07%, 7.92%, 14.51%ß 2022-03-28, 3.08%, 13.52%, 12.37%, 10.08%, 13.95%, -6.1%, 12.93%ß 2022-03-29, -0.44%, 19.34%, 16.63%, 12.68%, 11.39%, -1.16%, 9.28%ß 2022-03-30, 0.8%, 17.93%, 15.49%, 11.9%, 12.33%, 1.4%, 10.16%"",
""currDataVixVec"": ""22.1, 24, 24.81, 25.55, 25.8, 26.15, 26.43, 25.85, 0.086, 0.0344, 1.9, 0.81, 0.74, 0.25, 0.35, 0.28, -0.58, 0.88, 0.2933, 0.086, 0.0337, 0.0298, 0.0098, 0.0136, 0.0107, -0.0219, 0.0344, 0.0115"",
""currDataDaysVixVec"": ""21, 49, 77, 112, 140, 175, 203, 231, 21, 112, 21, 49, 77, 112, 140, 175, 203"",
""prevDataVixVec"": ""21.7532, 23.7145, 24.6018, 25.3995, 25.5995, 26.0173, 26.3708, 25.825, 0.0902, 0.0382, 1.9613, 0.8873, 0.7977, 0.2, 0.4178, 0.3535, -0.5458"",
""currDataDiffVixVec"": ""0.3468, 0.2855, 0.2082, 0.1505, 0.2005, 0.1327, 0.0592, 0.025, -0.0042, -0.0038, -0.0613, -0.0773, -0.0577, 0.05, -0.0678, -0.0735, -0.0342"",
""currDataPercChVixVec"": ""0.0159, 0.012, 0.0085, 0.0059, 0.0078, 0.0051, 0.0022, 0.001, -0.0465, -0.0993, -0.0313, -0.0871, -0.0723, 0.25, -0.1623, -0.2079, 0.0627"",
""spotVixVec"": ""19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56, 19.56""
}";
            return mockupTestResponse;
        }
// Under Development - Daya.
        // public object RenewedUberGoogleApiGsheet(string p_usedGSheetRef)
        // {
        //     Utils.Logger.Info("SINGoogleApiGsheet() BEGIN");

        //      string? valuesFromGSheetStr = "Error. Make sure GoogleApiKeyKey, GoogleApiKeyKey is in SQLab.WebServer.SQLab.NoGitHub.json !";
        //     if (!String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) && !String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
        //     {
        //         valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync(p_usedGSheetRef + Utils.Configuration["Google:GoogleApiKeyKey"], 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
        //         if (valuesFromGSheetStr == null)
        //             valuesFromGSheetStr = "Error in DownloadStringWithRetry().";
        //     }
            
        //     Utils.Logger.Info("SINGoogleApiGsheet() END");
        //     return Content($"<HTML><body>SINGoogleApiGsheet() finished OK. <br> Received data: '{valuesFromGSheetStr}'</body></HTML>", "text/html");
        // }

        //     //Selecting, splitting data got from GSheet
        // public static Tuple<double[], DateTime[], string[,], int[], int[], int[], string[]> GSheetConverter(string? p_gSheetString, string[] p_allAssetList)
        // {
        // if (p_gSheetString != null)
        //     {
        //     string[] gSheetTableRows = p_gSheetString.Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);
        //     string currPosRaw = gSheetTableRows[3];
        //     currPosRaw = currPosRaw.Replace("\n", "").Replace("]", "").Replace("\",", "BRB").Replace("\"", "").Replace(" ", "").Replace(",", "");
        //     string[] currPos = currPosRaw.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);
        //     string[] currPosAP = new string[p_allAssetList.Length];
        //     Array.Copy(currPos, 2, currPosAP, 0, p_allAssetList.Length);
        //     int currPosDate = Int32.Parse(currPos[0]);
        //     int currPosCash = Int32.Parse(currPos[^3]);
        //     int[] currPosDateCash = new int[] { currPosDate, currPosCash };
        //     int[] currPosAssets = Array.ConvertAll(currPosAP, int.Parse);


        //     p_gSheetString = p_gSheetString.Replace("\n", "").Replace("]", "").Replace("\"", "").Replace(" ", "").Replace(",,", ",0,").Replace(",,", ",0,");
        //     gSheetTableRows = p_gSheetString.Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);

        //     string[,] gSheetCodes = new string[gSheetTableRows.Length - 4, currPos.Length];
        //     string[] gSheetCodesH = new string[currPos.Length];
        //     for (int iRows = 0; iRows < gSheetCodes.GetLength(0); iRows++)
        //     {
        //         gSheetCodesH = gSheetTableRows[iRows + 4].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
        //         for (int jCols = 0; jCols < gSheetCodes.GetLength(1); jCols++)
        //         {
        //             gSheetCodes[iRows, jCols] = gSheetCodesH[jCols];
        //         }
        //     }

        //     gSheetCodes[gSheetCodes.GetLength(0) - 1, gSheetCodes.GetLength(1) - 1] = gSheetCodesH[^1][..gSheetCodesH[^1].IndexOf('}')];

        //     double[] gSheetDateVec = new double[gSheetCodes.GetLength(0)];
        //     for (int iRows = 0; iRows < gSheetDateVec.Length; iRows++)
        //     {
        //         gSheetDateVec[iRows] = Double.Parse(gSheetCodes[iRows, 0]);
        //     }

        //     DateTime[] gSheetRealDateVec = new DateTime[gSheetCodes.GetLength(0)];
        //     for (int iRows = 0; iRows < gSheetRealDateVec.Length; iRows++)
        //     {
        //         gSheetRealDateVec[iRows] = DateTime.Parse(gSheetCodes[iRows, 1]);
        //     }

        //     string[,] gSheetCodesAssets = new string[gSheetCodes.GetLength(0), p_allAssetList.Length + 1];
        //     for (int iRows = 0; iRows < gSheetCodesAssets.GetLength(0); iRows++)
        //     {
        //         for (int jCols = 0; jCols < gSheetCodesAssets.GetLength(1); jCols++)
        //         {
        //             gSheetCodesAssets[iRows, jCols] = gSheetCodes[iRows, jCols + 3];
        //         }
        //     }

        //     string[] gSheetEventFinalSignal = new string[gSheetCodes.GetLength(0)];
        //     for (int iRows = 0; iRows < gSheetEventFinalSignal.Length; iRows++)
        //     {
        //         gSheetEventFinalSignal[iRows] = gSheetCodes[iRows, 2];
        //     }

        //     int[] gSheetEventCodes = new int[gSheetCodes.GetLength(0)];
        //     for (int iRows = 0; iRows < gSheetEventCodes.Length; iRows++)
        //     {
        //         gSheetEventCodes[iRows] = Int32.Parse(gSheetCodes[iRows, gSheetCodes.GetLength(1) - 1]);
        //     }


        //     Tuple<double[], DateTime[], string[,], int[], int[], int[], string[]> gSheetResFinal = Tuple.Create(gSheetDateVec, gSheetRealDateVec, gSheetCodesAssets, gSheetEventCodes, currPosDateCash, currPosAssets, gSheetEventFinalSignal);

        //     return gSheetResFinal;
        // }
        // throw new NotImplementedException();
        // }
    }
}