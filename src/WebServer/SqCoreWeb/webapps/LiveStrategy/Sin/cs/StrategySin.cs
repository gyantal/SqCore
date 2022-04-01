using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FinTechCommon;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SqCommon;
using System.Net.Http;
using System.Text;

namespace SqCoreWeb.Controllers
{    
    [ApiController]
    [Route("[controller]")]
    [ResponseCache(CacheProfileName = "NoCache")]
    public class StrategySinController : ControllerBase
    {
        public class ExampleMessage
        {
            public string MsgType { get; set; } = string.Empty;

            public string StringData { get; set; } = string.Empty;
            public DateTime DateOrTime { get; set; }

            public int IntData { get; set; }

            public int IntDataFunction => 32 + (int)(IntData / 0.5556);
        }

        public StrategySinController()
        {
        }

        // [HttpGet] // only 1 HttpGet attribute should be in the Controller (or you have to specify in it how to resolve)
        public IEnumerable<ExampleMessage> Get_old()
        {
            Thread.Sleep(5000);     // intentional delay to simulate a longer process to crunch data. This can be removed.

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
            Thread.Sleep(3000);     // intentional delay to simulate a longer process to crunch data. This can be removed.

            // string titleString = "Monthly rebalance, <b>The Charmat Rebalancing Method</b> (Trend following with Percentile Channel weights), Cash to TLT";
            string usedGSheetRef = "https://sheets.googleapis.com/v4/spreadsheets/1JXMbEMAP5AOqB1FjdM8jpptXfpuOno2VaFVYK8A1eLo/values/A1:Z2000?key=";
            string usedGSheet2Ref = "https://docs.google.com/spreadsheets/d/1JXMbEMAP5AOqB1FjdM8jpptXfpuOno2VaFVYK8A1eLo/edit?usp=sharing";
            string usedGDocRef = "https://docs.google.com/document/d/1dBHg3-McaHeCtxCTZdJhTKF5NPaixXYjEngZ4F2_ZBE/edit?usp=sharing";
            // Utils.Logger.Info("The title string",titleString);
            
            //Get, split and convert GSheet data
            var gSheetReadResult = SINGoogleApiGsheet(usedGSheetRef);
            string? content = ((ContentResult)gSheetReadResult).Content;
            string? gSheetString = content;
            Tuple<int[], string[], int[], bool[], int[], double[]> gSheetResToFinCalc = GSheetConverter(gSheetString);
            string[] allAssetList = gSheetResToFinCalc.Item2;
            // for debugging purpose
            Utils.Logger.Info(usedGSheet2Ref, usedGDocRef, allAssetList);
            // multiline verbatim string literal format in C#
            string mockupTestResponse = @"{
""titleCont"": ""Monthly rebalance, <b>The Charmat Rebalancing Method</b> (Trend following with Percentile Channel weights), Cash to TLT"",
""requestTime"": ""Request time (UTC): 2022-03-24 21:30:38"",
""lastDataTime"": ""Last data time (UTC): Close price on 2022-03-24"",
""currentPV"": ""546,965"",
""currentPVDate"": ""2022-01-31"",
""gDocRef"": ""https://docs.google.com/document/d/1dBHg3-McaHeCtxCTZdJhTKF5NPaixXYjEngZ4F2_ZBE/edit?usp=sharing"",
""gSheetRef"": ""https://docs.google.com/spreadsheets/d/1JXMbEMAP5AOqB1FjdM8jpptXfpuOno2VaFVYK8A1eLo/edit?usp=sharing"",
""dailyProfSig"": ""+$"",
""dailyProfAbs"": ""2,917"",
""dailyProfPerc"": ""0.54"",
""dailyProfString"": ""posDaily"",
""monthlyProfSig"": ""+$"",
""monthlyProfAbs"": ""13,857"",
""monthlyProfPerc"": ""2.6"",
""monthlyProfString"": ""posMonthly"",
""yearlyProfSig"": ""+$"",
""yearlyProfAbs"": ""9,015"",
""yearlyProfPerc"": ""1.68"",
""yearlyProfString"": ""posYearly"",
""currBondPerc"": ""0%"",
""nextBondPerc"": ""0%"",
""leverage"": ""100%"",
""maxBondPerc"": ""0%"",
""assetNames"": ""ATVI, BA, BTI, BUD, CARA, CGC, DEO, EA, GD, HEI, HEINY, HON, JAZZ, LHX, LMT, LVS, MO, MTCH, NOC, PM, PRNDY, RICK, RTX, SCHYY, SONY, STZ, TLRY, WYNN, TLT"",
""assetNames2"": ""ATVI, BA, BTI, BUD, CARA, CGC, DEO, EA, GD, HEI, HEINY, HON, JAZZ, LHX, LMT, LVS, MO, MTCH, NOC, PM, PRNDY, RICK, RTX, SCHYY, SONY, STZ, TLRY, WYNN, TLT, Cash"",
""pastPerfDaysNum"": ""1, 5, 10, 21, 63, 126, 252"",
""pastPerfDaysName"": ""1-Day, 1-Week, 2-Weeks, 1-Month, 3-Months, 6-Months, 1-Year"",
""currPosNum"": ""50, 0, 1000, 230, 0, 0, 0, 0, 235, 0, 360, 0, 65, 0, 100, 0, 850, 0, 35, 455, 0, 100, 400, 0, 0, 85, 0, 0, 0, $188K"",
""currPosVal"": ""$4K, $0K, $43K, $13K, $0K, $0K, $0K, $0K, $57K, $0K, $17K, $0K, $10K, $0K, $45K, $0K, $44K, $0K, $16K, $42K, $0K, $6K, $41K, $0K, $0K, $19K, $0K, $0K, $0K, $188K"",
""nextPosNum"": ""735, 0, 309, 0, 0, 0, 0, 0, 117, 194, 0, 0, 133, 81, 47, 0, 713, 0, 42, 0, 0, 0, 281, 0, 0, 0, 0, 0, 0, $269K"",
""nextPosVal"": ""$58K, $0K, $13K, $0K, $0K, $0K, $0K, $0K, $28K, $30K, $0K, $0K, $21K, $21K, $21K, $0K, $37K, $0K, $19K, $0K, $0K, $0K, $28K, $0K, $0K, $0K, $0K, $0K, $0K, $269K"",
""posNumDiff"": ""685, 0, -691, -230, 0, 0, 0, 0, -118, 194, -360, 0, 68, 81, -53, 0, -137, 0, 7, -455, 0, -100, -119, 0, 0, -85, 0, 0, 0, $81K"",
""posValDiff"": ""$54K, $0K, $-30K, $-13K, $0K, $0K, $0K, $0K, $-28K, $30K, $-17K, $0K, $11K, $21K, $-24K, $0K, $-7K, $0K, $3K, $-42K, $0K, $-6K, $-12K, $0K, $0K, $-19K, $0K, $0K, $0K, $81K"",
""nextTradingDay"": ""2022-03-25"",
""currPosDate"": ""2022-01-31"",
""assetChangesToChartMtx"": ""0.54%, 0.77%, -1.61%, -1.5%, 24.02%, 6.47%, -12.07%ß 1.54%, -0.68%, 5.89%, -3.83%, -6.34%, -14.56%, -23.58%ß 2.04%, 4.17%, 4.77%, -7.23%, 18.44%, 21.5%, 17.98%ß -0.09%, -1.77%, 4.69%, -6.37%, -2.86%, -0.29%, -4.65%ß -5.04%, -5.51%, -6.73%, 14.89%, -6.28%, -27.44%, -41.24%ß 22.28%, 28.06%, 35.47%, 25.47%, -7.47%, -40.45%, -73.37%ß 1.12%, 2.29%, 12.1%, 1.1%, -7.03%, 4.97%, 23.83%ß 0.75%, 0.31%, 1.53%, -0.91%, -4.49%, -1.68%, -3.07%ß 0.55%, 4.55%, 2.33%, 11.43%, 18.94%, 23.92%, 37.15%ß 2.3%, 4.59%, 5.52%, 12.66%, 10.3%, 17.02%, 26.85%ß 3.53%, 1.63%, 4.81%, -8.69%, -12.79%, -10.12%, -5.49%ß 1.38%, 1.59%, 5.77%, 9.56%, -2.86%, -10.37%, -7.17%ß 0.72%, 1.36%, 2.85%, 18.93%, 24.99%, 18.67%, -4.23%ß 0.63%, 3.25%, 1.13%, 18.77%, 24.25%, 16.65%, 31.12%ß 1.04%, 4.97%, 1.25%, 16.48%, 32.02%, 32.7%, 28.22%ß 4.3%, 6.53%, 3.13%, -11.83%, 7.51%, 6.79%, -34.44%ß -1.71%, 1.2%, 2.11%, 0.69%, 11.58%, 9.23%, 10.07%ß 4.46%, 12.39%, 12.8%, 2.41%, -18.52%, -30.83%, -20.46%ß 1.92%, 6.22%, 2.44%, 18.45%, 20.4%, 30.89%, 44.22%ß 1.92%, -0.35%, 1.21%, -14.36%, 0.1%, -5.93%, 8.91%ß -0.32%, -1.49%, 4.29%, -5.99%, -14.27%, -7.71%, 11.25%ß 1.68%, 1.42%, 5.26%, 3.06%, -11.42%, -11.09%, -0.94%ß 0.04%, 3.6%, 2.8%, 10.4%, 21.13%, 18.08%, 33.71%ß -0.04%, 1.19%, -2.62%, -20.84%, 4.12%, 10.62%, -53.35%ß 3.28%, 2.82%, 7.38%, 6.18%, -12.62%, -6.87%, 4.79%ß 0.86%, 2.11%, 6%, 6.58%, -6.59%, 6.52%, -0.72%ß 37.76%, 45.66%, 45.39%, 33.33%, 1.81%, -34.44%, -65.48%ß 3.45%, 5.77%, 6.3%, -6.7%, -5.26%, -2.96%, -36.49%ß -0.91%, -1.16%, -3.09%, -4.54%, -12.79%, -11.5%, -3.49%"",
""assetScoresMtx"": ""50%, 10.69%ß -100%, 0%ß 50%, 2.43%ß -100%, 0%ß 0%, 0%ß 0%, 0%ß -50%, 0%ß -100%, 0%ß 100%, 5.15%ß 100%, 5.51%ß -100%, 0%ß -50%, 0%ß 100%, 3.89%ß 100%, 3.79%ß 100%, 3.9%ß -100%, 0%ß 100%, 6.82%ß -100%, 0%ß 100%, 3.49%ß -100%, 0%ß -100%, 0%ß -100%, 0%ß 100%, 5.19%ß -100%, 0%ß -50%, 0%ß -50%, 0%ß 0%, 0%ß -100%, 0%ß ---, 0%""
}";
            return mockupTestResponse;
        }

        public static Tuple<int[], string[], int[], bool[], int[], double[]> GSheetConverter(string? p_gSheetString)
        {
            if (p_gSheetString != null)
            {
                string[] gSheetTableRows = p_gSheetString.Split(new string[] { "[" }, StringSplitOptions.RemoveEmptyEntries);
                int assNum = gSheetTableRows.Length - 9;
                string[] assNameString = new string[assNum];
                string[] currPosAssString = new string[assNum];
                string[] currAssIndString = new string[assNum];
                for (int iRows = 4; iRows < gSheetTableRows.Length - 5; iRows++)
                {
                    string currPosRaw = gSheetTableRows[iRows];
                    currPosRaw = currPosRaw.Replace("\n", "").Replace("]", "").Replace("\",", "BRB").Replace("\"", "").Replace(" ", "").Replace(",", "");
                    string[] currPos = currPosRaw.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);
                    assNameString[iRows - 4] = currPos[0];
                    currPosAssString[iRows - 4] = currPos[1];
                    currAssIndString[iRows - 4] = currPos[2];
                }

                string currDateRaw = gSheetTableRows[2];
                currDateRaw = currDateRaw.Replace("\n", "").Replace("]", "").Replace("\",", "BRB").Replace("\"", "").Replace(" ", "").Replace(",", "");
                string[] currDateVec = currDateRaw.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);

                string currDateRaw2 = gSheetTableRows[3];
                currDateRaw2 = currDateRaw2.Replace("\n", "").Replace("]", "").Replace("\",", "BRB").Replace("\"", "").Replace(" ", "").Replace(",", "");
                string[] currDateVec2 = currDateRaw2.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);

                string currCashRaw = gSheetTableRows[^5];
                currCashRaw = currCashRaw.Replace("\n", "").Replace("]", "").Replace("\",", "BRB").Replace("\"", "").Replace(" ", "").Replace(",", "");
                string[] currCashVec = currCashRaw.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);

                string[] prevPVString = new string[4];
                for (int iRows = 0; iRows < prevPVString.Length; iRows++)
                {
                    string currPosRaw = gSheetTableRows[gSheetTableRows.Length - 4 + iRows];
                    currPosRaw = currPosRaw.Replace("\n", "").Replace("]", "").Replace("\",", "BRB").Replace("\"", "").Replace(" ", "").Replace(",", "");
                    string[] currPos = currPosRaw.Split(new string[] { "BRB" }, StringSplitOptions.RemoveEmptyEntries);
                    prevPVString[iRows] = currPos[1];
                }

                int currPosDate = Int32.Parse(currDateVec[1]);
                int currPosCash = Int32.Parse(currCashVec[1]);
                int[] currPosDateCash = new int[] { currPosDate, currPosCash };
                double leverage = Double.Parse(currDateVec[2]);
                double maxBondPerc = Double.Parse(currDateVec2[2]);
                double[] levMaxBondPerc = new double[] { leverage, maxBondPerc };

                int[] currPosAssets = Array.ConvertAll(currPosAssString, int.Parse);
                bool[] currAssInd = currAssIndString.Select(chr => chr == "1").ToArray();
                int[] prevPV = Array.ConvertAll(prevPVString, int.Parse);


                Tuple<int[], string[], int[], bool[], int[], double[]> gSheetResFinal = Tuple.Create(currPosDateCash, assNameString, currPosAssets, currAssInd, prevPV, levMaxBondPerc);

                return gSheetResFinal;
            }
            throw new NotImplementedException();
        }

        public object SINGoogleApiGsheet(string p_usedGSheetRef)
        {
            Utils.Logger.Info("SINGoogleApiGsheet() BEGIN");

            string valuesFromGSheetStr = "Error. Make sure GoogleApiKeyKey, GoogleApiKeyKey is in SQLab.WebServer.SQLab.NoGitHub.json !";
            if (!String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) && !String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
            {
                if (DownloadStringWithRetry(out valuesFromGSheetStr, p_usedGSheetRef + Utils.Configuration["Google:GoogleApiKeyKey"], 3, TimeSpan.FromSeconds(2), true))
                    if (valuesFromGSheetStr == null)
                        valuesFromGSheetStr = "Error in DownloadStringWithRetry().";
            }
            
            Utils.Logger.Info("SINGoogleApiGsheet() END");
            return Content($"<HTML><body>SINGoogleApiGsheet() finished OK. <br> Received data: '{valuesFromGSheetStr}'</body></HTML>", "text/html");
        }
        public static bool DownloadStringWithRetry(out string p_webpage, string p_url, int p_nRetry, TimeSpan p_sleepBetweenRetries, bool p_throwExceptionIfUnsuccesfull)
        {
            p_webpage = String.Empty;
            int nDownload = 0;
            do
            {

                try
                {
                    nDownload++;
                    p_webpage = new HttpClient().GetStringAsync(p_url).Result;
                    Utils.Logger.Debug(String.Format("DownloadStringWithRetry() OK:{0}, nDownload-{1}, Length of reply:{2}", p_url, nDownload, p_webpage.Length));
                    return true;
                }
                catch (Exception ex)
                {
                    // it is quite expected that sometimes (once per month), there is a problem:
                    // "The operation has timed out " or "Unable to connect to the remote server" exceptions
                    // Don't raise Logger.Error() after the first attempt, because it is not really Exceptional, and an Error email will be sent
                    Utils.Logger.Info(ex, "Exception in DownloadStringWithRetry()" + p_url + ":" + nDownload + ": " + ex.Message);
                    Thread.Sleep(p_sleepBetweenRetries);
                    if ((nDownload >= p_nRetry) && p_throwExceptionIfUnsuccesfull)
                        throw;  // if exception still persist after many tries, rethrow it to caller
                }
            } while (nDownload < p_nRetry);

            return false;
        }
    }
        
        //     public static Tuple<IList<List<SQLab.Controllers.QuickTester.Strategies.DailyData>>, List<SQLab.Controllers.QuickTester.Strategies.DailyData>> DataSQDBG(string[] p_allAssetList)
        // {
        //     return null;
        //     MemDb.gMemDb.GetSdaPriorClosesFromHist();
        //     throw new NotImplementedException();
        // }
}