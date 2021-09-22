using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SqCommon;
using System.Text;

namespace SqCoreWeb.Controllers
{
    struct VixCentralRec2
    {
        public DateTime Date;
        public int FirstMonth;
        public double F1;
        public double F2;
        public double F3;
        public double F4;
        public double F5;
        public double F6;
        public double F7;
        public double F8;
        public double STCont;
        public double LTCont;

        public DateTime NextExpiryDate;
        public int F1expDays;
        public int F2expDays;
        public int F3expDays;
        public int F4expDays;
        public int F5expDays;
        public int F6expDays;
        public int F7expDays;
        public int F8expDays;

        public override string ToString()
        {
            return $"{Date.ToString("yyyy-MM-dd")},{FirstMonth}, {F1:F3}, {F2:F3}, {F3:F3}, {F4:F3}, {F5:F3}, {F6:F3}, {F7:F3}, {F8:F3}, {STCont:P2}, {LTCont:P2}, {NextExpiryDate.ToString("yyyy-MM-dd")}, {F1expDays}, {F2expDays}, {F3expDays}, {F4expDays}, {F5expDays}, {F6expDays}, {F7expDays}, {F8expDays} ";
        }
    }

    //--[Route("[controller]")]
    public class ContangoVisualizerDataController : Controller
    {
// #if !DEBUG
//         [Authorize]
// #endif
        public ActionResult Index(int commo)
        {
            switch (commo)
            {
                case 1:
                    return Content(GetStrVIX(), "text/html");
                case 2:
                    return Content(GetStrOIL(), "text/html");
                case 3:
                    return Content(GetStrGAS(), "text/html");

            }
            return Content(GetStr2(), "text/html");
        }

        public string GetStr2()
        {
            return "Error";
        }

        public string GetStrVIX()
        {
            
            //Downloading live data from vixcentral.com.
            string? webpageLive = Utils.DownloadStringWithRetryAsync("http://vixcentral.com", 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
            if (webpageLive == null)
                return "Error in live data";

            string? webpageLiveAjax = Utils.DownloadStringWithRetryAsync("http://vixcentral.com/ajax_update", 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
            if (webpageLiveAjax == null)
                return "Error in live data";
            
            string[] resuRows = webpageLiveAjax.Split(new string[] { "[", "]" }, StringSplitOptions.RemoveEmptyEntries);
            string[] liveFuturesPrices = resuRows[4].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            string[] spotVixPrices = resuRows[16].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            double spotVixValue = Double.Parse(spotVixPrices[0]);
            string[] futuresNextExps = resuRows[0].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            string liveFuturesNextExp = futuresNextExps[0].Substring(1,3);
            string[] liveFuturesTime = resuRows[2].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            string liveFuturesDataTime = (liveFuturesTime[0].Length>3)?liveFuturesTime[0].Substring(1,8):"99:99:99";

            //Selecting data from live data string.
            
            // string liveFuturesDataDT = string.Empty;
            // string liveFuturesDataDate = string.Empty;
            // string liveFuturesDataTime = string.Empty;
            // string liveFuturesData = string.Empty;
            string prevFuturesData = string.Empty;
            // string liveFuturesNextExp = string.Empty;
            // string spotVixData = string.Empty;
            string titleVIX = "VIX Futures Term Structure";
            string dataSourceVIX = "http://vixcentral.com";

            // int startPosLiveDate = webpageLive.IndexOf("var time_data_var=['") + "var time_data_var=['".Length;
            // int startPosLive = webpageLive.IndexOf("var last_data_var=[",startPosLiveDate) + "var last_data_var=[".Length;
            // int endPosLive = webpageLive.IndexOf("];last_data_var=clean_array(last_data_var);", startPosLive);
            int startPosPrev = webpageLive.IndexOf("];var previous_close_var=[", 0) + "];var previous_close_var=[".Length;
            int endPosPrev = webpageLive.IndexOf("];var contango_graph_exists=", startPosPrev);
            // int nextExpLiveMonth = webpageLive.IndexOf("var mx=['", endPosPrev) + "var mx=['".Length;
            // int startSpotVix = webpageLive.IndexOf("{id:'VIX_Index',name:'VIX Index',legendIndex:9,lineWidth:2,color:'green',dashStyle:'LongDash',marker:{enabled:false},dataLabels:{enabled:true,align:'left',x:5,y:4,formatter:function(){if(this.point.x==this.series.data.length-1){return Highcharts.numberFormat(this.y,2);}else{return null;}}},data:[", nextExpLiveMonth) + "{id:'VIX_Index',name:'VIX Index',legendIndex:9,lineWidth:2,color:'green',dashStyle:'LongDash',marker:{enabled:false},dataLabels:{enabled:true,align:'left',x:5,y:4,formatter:function(){if(this.point.x==this.series.data.length-1){return Highcharts.numberFormat(this.y,2);}else{return null;}}},data:[".Length;
            // int endSpotVix = webpageLive.IndexOf("]},{id:'VXV_Index',name:'VXV Index',legendIndex:10,lineWidth:2", startSpotVix);
            // liveFuturesDataDT = webpageLive.Substring(startPosLiveDate, 20);
            // liveFuturesDataDT = webpageLive.Substring(startPosLiveDate, 8);
            // liveFuturesNextExp = webpageLive.Substring(nextExpLiveMonth, 3);
            // liveFuturesData = webpageLive.Substring(startPosLive, endPosLive - startPosLive);
            prevFuturesData = webpageLive.Substring(startPosPrev, endPosPrev - startPosPrev);
            // spotVixData = webpageLive.Substring(startSpotVix, endSpotVix - startSpotVix);

            // liveFuturesDataDate = liveFuturesDataDT.Substring(0,10);
            // liveFuturesDataTime = liveFuturesDataDT.Substring(12, 8) + " EST";
            
            // string[] liveFuturesPrices = liveFuturesData.Split(new string[] { ","}, StringSplitOptions.RemoveEmptyEntries);
            int lengthLiveFuturesPrices = liveFuturesPrices.Length;
            string[] prevFuturesPrices = prevFuturesData.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            // string[] spotVixPrices = spotVixData.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            // double spotVixValue = Double.Parse(spotVixPrices[0]);

            string[] monthsNumList = {"Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"};
            int monthsNum = Array.IndexOf(monthsNumList,liveFuturesNextExp)+1;
          

            // DateTime liveDateTime;
            DateTime timeNowETVIX = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            int dayOfWeekVIX;
            dayOfWeekVIX = Convert.ToInt32(timeNowETVIX.DayOfWeek);
            if (dayOfWeekVIX == 0)
            {
                timeNowETVIX = timeNowETVIX.AddDays(-2);
            }
            else if (dayOfWeekVIX == 6)
            {
                timeNowETVIX = timeNowETVIX.AddDays(-1);
            }
            string liveDate = string.Empty;
            // liveDateTime = DateTime.Parse(liveFuturesDataDate);
            liveDate = timeNowETVIX.ToString("yyyy-MM-dd");

            // liveFuturesDataTime = liveFuturesDataDT.Substring(0, 8) + " EST";

            //Sorting historical data.
            VixCentralRec2[] vixCentralRec = new VixCentralRec2[2];
            
                vixCentralRec[0].Date = DateTime.Parse(liveDate);
                vixCentralRec[0].FirstMonth = monthsNum;
                vixCentralRec[0].F1 = Double.Parse(liveFuturesPrices[0]);
                vixCentralRec[0].F2 = Double.Parse(liveFuturesPrices[1]);
                vixCentralRec[0].F3 = Double.Parse(liveFuturesPrices[2]);
                vixCentralRec[0].F4 = Double.Parse(liveFuturesPrices[3]);
                vixCentralRec[0].F5 = Double.Parse(liveFuturesPrices[4]);
                vixCentralRec[0].F6 = Double.Parse(liveFuturesPrices[5]);
                vixCentralRec[0].F7 = Double.Parse(liveFuturesPrices[6]);
                vixCentralRec[0].F8 = (lengthLiveFuturesPrices == 8 ) ? Double.Parse(liveFuturesPrices[7]) : 0;
                vixCentralRec[0].STCont = vixCentralRec[0].F2 / vixCentralRec[0].F1 - 1;
                vixCentralRec[0].LTCont = vixCentralRec[0].F7 / vixCentralRec[0].F4 - 1;

                vixCentralRec[1].Date = DateTime.Parse(liveDate);
                vixCentralRec[1].FirstMonth = monthsNum;
                vixCentralRec[1].F1 = Double.Parse(prevFuturesPrices[0]);
                vixCentralRec[1].F2 = Double.Parse(prevFuturesPrices[1]);
                vixCentralRec[1].F3 = Double.Parse(prevFuturesPrices[2]);
                vixCentralRec[1].F4 = Double.Parse(prevFuturesPrices[3]);
                vixCentralRec[1].F5 = Double.Parse(prevFuturesPrices[4]);
                vixCentralRec[1].F6 = Double.Parse(prevFuturesPrices[5]);
                vixCentralRec[1].F7 = Double.Parse(prevFuturesPrices[6]);
                vixCentralRec[1].F8 = (lengthLiveFuturesPrices == 8) ? Double.Parse(prevFuturesPrices[7]) : 0;
                vixCentralRec[1].STCont = vixCentralRec[1].F2 / vixCentralRec[1].F1 - 1;
                vixCentralRec[1].LTCont = vixCentralRec[1].F7 / vixCentralRec[1].F4 - 1;

            //Calculating futures expiration dates.

            var lastDataDay = vixCentralRec[0].Date;
            int lastDataYear = lastDataDay.Year;
            string lastData = lastDataDay.ToString("yyyy-MM-dd");

            var lengthExps = /*(lastDataYear - firstDataYear + 2)*/ 2 * 12;
            int[,] expDatesDat = new int[lengthExps,2];
            
            expDatesDat[0,0] = lastDataYear + 1;
            expDatesDat[0,1] = 12;

            for (int iRows = 1; iRows < expDatesDat.GetLength(0); iRows++)
            {
                decimal f = iRows / 12;
             expDatesDat[iRows,0] = lastDataYear - Decimal.ToInt32(Math.Floor(f))+1;
             expDatesDat[iRows,1] = 12 - iRows % 12;
            }

            DateTime[] expDates = new DateTime[expDatesDat.GetLength(0)];
            for (int iRows = 0; iRows < expDates.Length; iRows++)
            {
                DateTime thirdFriday = new DateTime(expDatesDat[iRows,0], expDatesDat[iRows,1], 15);
                while (thirdFriday.DayOfWeek != DayOfWeek.Friday)
                {
                    thirdFriday = thirdFriday.AddDays(1);
                }
                expDates[iRows] = thirdFriday.AddDays(-30);
                if (expDates[iRows]==DateTime.Parse("2014-03-19"))
                {
                    expDates[iRows] = DateTime.Parse("2014-03-18");
                }
            }

            //Calculating number of calendar days until expirations.
            for (int iRec = 0; iRec < vixCentralRec.Length; iRec++)
            {
                int index1 = Array.FindIndex(expDates, item => item <= vixCentralRec[iRec].Date);
                vixCentralRec[iRec].NextExpiryDate = expDates[index1-1];
                vixCentralRec[iRec].F1expDays = (expDates[index1 - 1] - vixCentralRec[iRec].Date).Days;
                vixCentralRec[iRec].F2expDays = (expDates[index1 - 2] - vixCentralRec[iRec].Date).Days;
                vixCentralRec[iRec].F3expDays = (expDates[index1 - 3] - vixCentralRec[iRec].Date).Days;
                vixCentralRec[iRec].F4expDays = (expDates[index1 - 4] - vixCentralRec[iRec].Date).Days;
                vixCentralRec[iRec].F5expDays = (expDates[index1 - 5] - vixCentralRec[iRec].Date).Days;
                vixCentralRec[iRec].F6expDays = (expDates[index1 - 6] - vixCentralRec[iRec].Date).Days;
                vixCentralRec[iRec].F7expDays = (expDates[index1 - 7] - vixCentralRec[iRec].Date).Days;
                vixCentralRec[iRec].F8expDays = (vixCentralRec[0].F8 > 0) ? (expDates[index1 - 8] - vixCentralRec[iRec].Date).Days:0;
            }
            
            string ret = Processing(vixCentralRec, expDates, liveDate, liveFuturesDataTime, spotVixValue, titleVIX, dataSourceVIX);

            return ret;

        }
        public string GetStrOIL()
        {

            //Downloading live data from cmegroup.com.
            string? webpageLive = Utils.DownloadStringWithRetryAsync("https://www.cmegroup.com/CmeWS/mvc/Quotes/Future/425/G", 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
            //bool isOkLive = Utils.DownloadStringWithRetry(out webpageLive, "http://www.cmegroup.com/trading/energy/crude-oil/light-sweet-crude.html", 3, TimeSpan.FromSeconds(2), true);
            if (webpageLive == null)
                return "Error in live data";

            //Selecting data from live data string.
            string[] liveFuturesDataVec = new string[8];
            int[] liveFuturesDataVecInd = new int[8];
            int startPosLiveB0 = webpageLive.IndexOf("\"last\":\"", 0) + "\"last\":\"".Length;
            int endPosLiveB0 = webpageLive.IndexOf("\",\"change\":", startPosLiveB0);
            string liveFuturesDataB0 = webpageLive.Substring(startPosLiveB0, endPosLiveB0 - startPosLiveB0);

            liveFuturesDataVec[0] = liveFuturesDataB0;
            liveFuturesDataVecInd[0] = endPosLiveB0;

            for (int iRows = 1; iRows < 8; iRows++)
            {
                int startPosLiveB = webpageLive.IndexOf("\"last\":\"", liveFuturesDataVecInd[iRows-1]) + "\"last\":\"".Length;
                int endPosLiveB = webpageLive.IndexOf("\",\"change\":", startPosLiveB);
                string liveFuturesDataB = webpageLive.Substring(startPosLiveB, endPosLiveB - startPosLiveB);

                liveFuturesDataVec[iRows] = liveFuturesDataB;
                liveFuturesDataVecInd[iRows] = endPosLiveB;
            }

            string[] prevFuturesDataVec = new string[8];
            int startPosPrevB0 = webpageLive.IndexOf("\"priorSettle\":\"", liveFuturesDataVecInd[0]) + "\"priorSettle\":\"".Length;
            int endPosPrevB0 = webpageLive.IndexOf("\",\"open\":\"", startPosPrevB0);
            string prevFuturesDataB0 = webpageLive.Substring(startPosPrevB0, endPosPrevB0 - startPosPrevB0);

            prevFuturesDataVec[0] = prevFuturesDataB0;
            
            for (int iRows = 1; iRows < 8; iRows++)
            {
                int startPosPrevB = webpageLive.IndexOf("\"priorSettle\":\"", liveFuturesDataVecInd[iRows]) + "\"priorSettle\":\"".Length;
                int endPosPrevB = webpageLive.IndexOf("\",\"open\":\"", startPosPrevB);
                string prevFuturesDataB = webpageLive.Substring(startPosPrevB, endPosPrevB - startPosPrevB);

                prevFuturesDataVec[iRows] = prevFuturesDataB;
            }

            string liveFuturesDataDT = string.Empty;
            string liveFuturesDataDate = string.Empty;
            string liveFuturesDataTime = string.Empty;
            string liveFuturesNextExp = string.Empty;
            string futCodeNext = string.Empty;
            string spotVixData = string.Empty;
            string titleOIL = "OIL Futures Term Structure";
            string dataSourceOIL = "https://www.cmegroup.com/trading/energy/crude-oil/light-sweet-crude.html";

            int startPosLiveDate = webpageLive.IndexOf("\"updated\":\"",liveFuturesDataVecInd[0]) + "\"updated\":\"".Length;
            liveFuturesDataDT = webpageLive.Substring(startPosLiveDate, 29);
            liveFuturesDataDate = liveFuturesDataDT.Substring(18, 11);
            liveFuturesDataTime = liveFuturesDataDT.Substring(0, 8) + " CT";

            int nextExpLiveMonth = webpageLive.IndexOf("\"expirationMonth\":\"", 0) + "\"expirationMonth\":\"".Length;
            liveFuturesNextExp = webpageLive.Substring(nextExpLiveMonth, 3);

            int futCodeInd = webpageLive.IndexOf("\"escapedQuoteCode\":\"", endPosPrevB0) + "\"escapedQuoteCode\":\"".Length;
            futCodeNext = webpageLive.Substring(futCodeInd, 3);

            //Downloading expiration dates from cmegroup.com.
            string? webpageLiveExp = Utils.DownloadStringWithRetryAsync("https://www.cmegroup.com/CmeWS/mvc/ProductCalendar/Future/425", 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
            if (webpageLiveExp == null)
                return "Error in live data";

            string[] liveFuturesDataExpVec = new string[8];
            int[] liveFuturesDataExpVecInd = new int[8];
            int startPosLiveExpB0Ass= webpageLiveExp.IndexOf(futCodeNext, 0);
            int startPosLiveExpB0 = webpageLiveExp.IndexOf("\"lastTrade\":\"", startPosLiveExpB0Ass) + "\"lastTrade\":\"".Length;
            int endPosLiveExpB0 = webpageLiveExp.IndexOf(",\"settlement", startPosLiveExpB0);
            string liveFuturesDataExpB0 = webpageLiveExp.Substring(startPosLiveExpB0, endPosLiveExpB0 - startPosLiveExpB0-1);

            liveFuturesDataExpVec[0] = liveFuturesDataExpB0;
            liveFuturesDataExpVecInd[0] = endPosLiveExpB0;

            for (int iRows = 1; iRows < 8; iRows++)
            {
                int startPosLiveExpBAss = webpageLiveExp.IndexOf("CL", liveFuturesDataExpVecInd[iRows - 1]);
                int startPosLiveExpB = webpageLiveExp.IndexOf("\"lastTrade\":\"", startPosLiveExpBAss) + "\"lastTrade\":\"".Length;
                int endPosLiveExpB = webpageLiveExp.IndexOf(",\"settlement", startPosLiveExpB);
                string liveFuturesDataExpB = webpageLiveExp.Substring(startPosLiveExpB, endPosLiveExpB - startPosLiveExpB-1);

                liveFuturesDataExpVec[iRows] = liveFuturesDataExpB;
                liveFuturesDataExpVecInd[iRows] = endPosLiveExpB;
            }


            string[] monthsNumList = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            int monthsNum = Array.IndexOf(monthsNumList, liveFuturesNextExp) + 1;

            DateTime[] expDates = new DateTime[8];
            for (int iRows = 0; iRows < expDates.Length; iRows++)
            {
                expDates[iRows] = DateTime.Parse(liveFuturesDataExpVec[iRows]);
            }
                   
            string[] liveFuturesPrices = liveFuturesDataVec;
            int lengthLiveFuturesPrices = liveFuturesPrices.Length;
            string[] prevFuturesPrices = prevFuturesDataVec;

            for (int iRows=0; iRows<8; iRows++)
            {
                if (String.Equals(liveFuturesPrices[iRows], "-"))
                {
                    liveFuturesPrices[iRows] = prevFuturesPrices[iRows];
                }
            }

            double spotVixValue = 0;/*Double.Parse(spotVixPrices[0]);*/

            


            DateTime liveDateTime;
            string liveDate = string.Empty;
            liveDateTime = DateTime.Parse(liveFuturesDataDate);
            liveDate = liveDateTime.ToString("yyyy-MM-dd");

            //Sorting historical data.
            VixCentralRec2[] vixCentralRec = new VixCentralRec2[2];

            vixCentralRec[0].Date = DateTime.Parse(liveDate);
            vixCentralRec[0].FirstMonth = monthsNum;
            vixCentralRec[0].F1 = Double.Parse(liveFuturesPrices[0]);
            vixCentralRec[0].F2 = Double.Parse(liveFuturesPrices[1]);
            vixCentralRec[0].F3 = Double.Parse(liveFuturesPrices[2]);
            vixCentralRec[0].F4 = Double.Parse(liveFuturesPrices[3]);
            vixCentralRec[0].F5 = Double.Parse(liveFuturesPrices[4]);
            vixCentralRec[0].F6 = Double.Parse(liveFuturesPrices[5]);
            vixCentralRec[0].F7 = Double.Parse(liveFuturesPrices[6]);
            vixCentralRec[0].F8 = (lengthLiveFuturesPrices == 8) ? Double.Parse(liveFuturesPrices[7]) : 0;
            vixCentralRec[0].STCont = vixCentralRec[0].F2 / vixCentralRec[0].F1 - 1;
            vixCentralRec[0].LTCont = vixCentralRec[0].F7 / vixCentralRec[0].F4 - 1;

            vixCentralRec[1].Date = DateTime.Parse(liveDate);
            vixCentralRec[1].FirstMonth = monthsNum;
            vixCentralRec[1].F1 = Double.Parse(prevFuturesPrices[0]);
            vixCentralRec[1].F2 = Double.Parse(prevFuturesPrices[1]);
            vixCentralRec[1].F3 = Double.Parse(prevFuturesPrices[2]);
            vixCentralRec[1].F4 = Double.Parse(prevFuturesPrices[3]);
            vixCentralRec[1].F5 = Double.Parse(prevFuturesPrices[4]);
            vixCentralRec[1].F6 = Double.Parse(prevFuturesPrices[5]);
            vixCentralRec[1].F7 = Double.Parse(prevFuturesPrices[6]);
            vixCentralRec[1].F8 = (lengthLiveFuturesPrices == 8) ? Double.Parse(prevFuturesPrices[7]) : 0;
            vixCentralRec[1].STCont = vixCentralRec[1].F2 / vixCentralRec[1].F1 - 1;
            vixCentralRec[1].LTCont = vixCentralRec[1].F7 / vixCentralRec[1].F4 - 1;

            
            //Calculating number of calendar days until expirations.
            for (int iRec = 0; iRec < vixCentralRec.Length; iRec++)
            {
                vixCentralRec[iRec].NextExpiryDate = expDates[0];
                vixCentralRec[iRec].F1expDays = (expDates[0] - vixCentralRec[iRec].Date).Days+1;
                vixCentralRec[iRec].F2expDays = (expDates[1] - vixCentralRec[iRec].Date).Days+1;
                vixCentralRec[iRec].F3expDays = (expDates[2] - vixCentralRec[iRec].Date).Days+1;
                vixCentralRec[iRec].F4expDays = (expDates[3] - vixCentralRec[iRec].Date).Days+1;
                vixCentralRec[iRec].F5expDays = (expDates[4] - vixCentralRec[iRec].Date).Days+1;
                vixCentralRec[iRec].F6expDays = (expDates[5] - vixCentralRec[iRec].Date).Days+1;
                vixCentralRec[iRec].F7expDays = (expDates[6] - vixCentralRec[iRec].Date).Days+1;
                vixCentralRec[iRec].F8expDays = (vixCentralRec[0].F8 > 0) ? (expDates[7] - vixCentralRec[iRec].Date).Days+1 : 0;
            }

            string ret = Processing(vixCentralRec, expDates, liveDate, liveFuturesDataTime, spotVixValue, titleOIL, dataSourceOIL);

            return ret;

        }

        public string GetStrGAS()
        {

            //Downloading live data from cmegroup.com.
            string? webpageLive = Utils.DownloadStringWithRetryAsync("https://www.cmegroup.com/CmeWS/mvc/Quotes/Future/444/G", 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
            //bool isOkLive = Utils.DownloadStringWithRetry("http://www.cmegroup.com/trading/energy/natural-gas/natural-gas.html", out webpageLive, 3, TimeSpan.FromSeconds(2), true);
            if (webpageLive == null)
                return "Error in live data";

            //Selecting data from live data string.
            string[] liveFuturesDataVec = new string[8];
            int[] liveFuturesDataVecInd = new int[8];
            int startPosLiveB0 = webpageLive.IndexOf("\"last\":\"", 0) + "\"last\":\"".Length;
            int endPosLiveB0 = webpageLive.IndexOf("\",\"change\":", startPosLiveB0);
            string liveFuturesDataB0 = webpageLive.Substring(startPosLiveB0, endPosLiveB0 - startPosLiveB0);

            liveFuturesDataVec[0] = liveFuturesDataB0;
            liveFuturesDataVecInd[0] = endPosLiveB0;

            for (int iRows = 1; iRows < 8; iRows++)
            {
                int startPosLiveB = webpageLive.IndexOf("\"last\":\"", liveFuturesDataVecInd[iRows - 1]) + "\"last\":\"".Length;
                int endPosLiveB = webpageLive.IndexOf("\",\"change\":", startPosLiveB);
                string liveFuturesDataB = webpageLive.Substring(startPosLiveB, endPosLiveB - startPosLiveB);

                liveFuturesDataVec[iRows] = liveFuturesDataB;
                liveFuturesDataVecInd[iRows] = endPosLiveB;
            }

            string[] prevFuturesDataVec = new string[8];
            int startPosPrevB0 = webpageLive.IndexOf("\"priorSettle\":\"", liveFuturesDataVecInd[0]) + "\"priorSettle\":\"".Length;
            int endPosPrevB0 = webpageLive.IndexOf("\",\"open\":\"", startPosPrevB0);
            string prevFuturesDataB0 = webpageLive.Substring(startPosPrevB0, endPosPrevB0 - startPosPrevB0);

            prevFuturesDataVec[0] = prevFuturesDataB0;

            for (int iRows = 1; iRows < 8; iRows++)
            {
                int startPosPrevB = webpageLive.IndexOf("\"priorSettle\":\"", liveFuturesDataVecInd[iRows]) + "\"priorSettle\":\"".Length;
                int endPosPrevB = webpageLive.IndexOf("\",\"open\":\"", startPosPrevB);
                string prevFuturesDataB = webpageLive.Substring(startPosPrevB, endPosPrevB - startPosPrevB);

                prevFuturesDataVec[iRows] = prevFuturesDataB;
            }

            string liveFuturesDataDT = string.Empty;
            string liveFuturesDataDate = string.Empty;
            string liveFuturesDataTime = string.Empty;
            string liveFuturesNextExp = string.Empty;
            string futCodeNext = string.Empty;
            string spotVixData = string.Empty;                
            string titleGAS = "GAS Futures Term Structure";
            string dataSourceGAS = "https://www.cmegroup.com/trading/energy/natural-gas/natural-gas.html";

            int startPosLiveDate = webpageLive.IndexOf("\"updated\":\"", liveFuturesDataVecInd[0]) + "\"updated\":\"".Length;
            liveFuturesDataDT = webpageLive.Substring(startPosLiveDate, 29);
            liveFuturesDataDate = liveFuturesDataDT.Substring(18, 11);
            liveFuturesDataTime = liveFuturesDataDT.Substring(0, 8) + " CT";

            int nextExpLiveMonth = webpageLive.IndexOf("\"expirationMonth\":\"", 0) + "\"expirationMonth\":\"".Length;
            liveFuturesNextExp = webpageLive.Substring(nextExpLiveMonth, 3);

            int futCodeInd = webpageLive.IndexOf("\"escapedQuoteCode\":\"", endPosPrevB0) + "\"escapedQuoteCode\":\"".Length;
            futCodeNext = webpageLive.Substring(futCodeInd, 3);

            //Downloading expiration dates from cmegroup.com.
            string? webpageLiveExp = Utils.DownloadStringWithRetryAsync("https://www.cmegroup.com/CmeWS/mvc/ProductCalendar/Future/444", 3, TimeSpan.FromSeconds(2), true).TurnAsyncToSyncTask();
            if (webpageLiveExp == null)
                return "Error in live data";

            string[] liveFuturesDataExpVec = new string[8];
            int[] liveFuturesDataExpVecInd = new int[8];
            int startPosLiveExpB0Ass = webpageLiveExp.IndexOf(futCodeNext, 0);
            int startPosLiveExpB0 = webpageLiveExp.IndexOf("\"lastTrade\":\"", startPosLiveExpB0Ass) + "\"lastTrade\":\"".Length;
            int endPosLiveExpB0 = webpageLiveExp.IndexOf(",\"settlement", startPosLiveExpB0);
            string liveFuturesDataExpB0 = webpageLiveExp.Substring(startPosLiveExpB0, endPosLiveExpB0 - startPosLiveExpB0 - 1);

            liveFuturesDataExpVec[0] = liveFuturesDataExpB0;
            liveFuturesDataExpVecInd[0] = endPosLiveExpB0;

            for (int iRows = 1; iRows < 8; iRows++)
            {
                int startPosLiveExpBAss = webpageLiveExp.IndexOf("NG", liveFuturesDataExpVecInd[iRows - 1]);
                int startPosLiveExpB = webpageLiveExp.IndexOf("\"lastTrade\":\"", startPosLiveExpBAss) + "\"lastTrade\":\"".Length;
                int endPosLiveExpB = webpageLiveExp.IndexOf(",\"settlement", startPosLiveExpB);
                string liveFuturesDataExpB = webpageLiveExp.Substring(startPosLiveExpB, endPosLiveExpB - startPosLiveExpB - 1);

                liveFuturesDataExpVec[iRows] = liveFuturesDataExpB;
                liveFuturesDataExpVecInd[iRows] = endPosLiveExpB;
            }

            string[] monthsNumList = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            int monthsNum = Array.IndexOf(monthsNumList, liveFuturesNextExp) + 1;

            DateTime[] expDates = new DateTime[8];
            for (int iRows = 0; iRows < expDates.Length; iRows++)
            {
                expDates[iRows] = DateTime.Parse(liveFuturesDataExpVec[iRows]);
            }

            string[] liveFuturesPrices = liveFuturesDataVec;
            int lengthLiveFuturesPrices = liveFuturesPrices.Length;
            string[] prevFuturesPrices = prevFuturesDataVec;

            double spotVixValue = 0;/*Double.Parse(spotVixPrices[0]);*/

            for (int iRows = 0; iRows < 8; iRows++)
            {
                if (String.Equals(liveFuturesPrices[iRows], "-"))
                {
                    liveFuturesPrices[iRows] = prevFuturesPrices[iRows];
                }
            }


            DateTime liveDateTime;
            string liveDate = string.Empty;
            liveDateTime = DateTime.Parse(liveFuturesDataDate);
            liveDate = liveDateTime.ToString("yyyy-MM-dd");

            //Sorting historical data.
            VixCentralRec2[] vixCentralRec = new VixCentralRec2[2];

            vixCentralRec[0].Date = DateTime.Parse(liveDate);
            vixCentralRec[0].FirstMonth = monthsNum;
            vixCentralRec[0].F1 = Double.Parse(liveFuturesPrices[0]);
            vixCentralRec[0].F2 = Double.Parse(liveFuturesPrices[1]);
            vixCentralRec[0].F3 = Double.Parse(liveFuturesPrices[2]);
            vixCentralRec[0].F4 = Double.Parse(liveFuturesPrices[3]);
            vixCentralRec[0].F5 = Double.Parse(liveFuturesPrices[4]);
            vixCentralRec[0].F6 = Double.Parse(liveFuturesPrices[5]);
            vixCentralRec[0].F7 = Double.Parse(liveFuturesPrices[6]);
            vixCentralRec[0].F8 = (lengthLiveFuturesPrices == 8) ? Double.Parse(liveFuturesPrices[7]) : 0;
            vixCentralRec[0].STCont = vixCentralRec[0].F2 / vixCentralRec[0].F1 - 1;
            vixCentralRec[0].LTCont = vixCentralRec[0].F7 / vixCentralRec[0].F4 - 1;

            vixCentralRec[1].Date = DateTime.Parse(liveDate);
            vixCentralRec[1].FirstMonth = monthsNum;
            vixCentralRec[1].F1 = Double.Parse(prevFuturesPrices[0]);
            vixCentralRec[1].F2 = Double.Parse(prevFuturesPrices[1]);
            vixCentralRec[1].F3 = Double.Parse(prevFuturesPrices[2]);
            vixCentralRec[1].F4 = Double.Parse(prevFuturesPrices[3]);
            vixCentralRec[1].F5 = Double.Parse(prevFuturesPrices[4]);
            vixCentralRec[1].F6 = Double.Parse(prevFuturesPrices[5]);
            vixCentralRec[1].F7 = Double.Parse(prevFuturesPrices[6]);
            vixCentralRec[1].F8 = (lengthLiveFuturesPrices == 8) ? Double.Parse(prevFuturesPrices[7]) : 0;
            vixCentralRec[1].STCont = vixCentralRec[1].F2 / vixCentralRec[1].F1 - 1;
            vixCentralRec[1].LTCont = vixCentralRec[1].F7 / vixCentralRec[1].F4 - 1;


            //Calculating number of calendar days until expirations.
            for (int iRec = 0; iRec < vixCentralRec.Length; iRec++)
            {
                vixCentralRec[iRec].NextExpiryDate = expDates[0];
                vixCentralRec[iRec].F1expDays = (expDates[0] - vixCentralRec[iRec].Date).Days + 1;
                vixCentralRec[iRec].F2expDays = (expDates[1] - vixCentralRec[iRec].Date).Days + 1;
                vixCentralRec[iRec].F3expDays = (expDates[2] - vixCentralRec[iRec].Date).Days + 1;
                vixCentralRec[iRec].F4expDays = (expDates[3] - vixCentralRec[iRec].Date).Days + 1;
                vixCentralRec[iRec].F5expDays = (expDates[4] - vixCentralRec[iRec].Date).Days + 1;
                vixCentralRec[iRec].F6expDays = (expDates[5] - vixCentralRec[iRec].Date).Days + 1;
                vixCentralRec[iRec].F7expDays = (expDates[6] - vixCentralRec[iRec].Date).Days + 1;
                vixCentralRec[iRec].F8expDays = (vixCentralRec[0].F8 > 0) ? (expDates[7] - vixCentralRec[iRec].Date).Days + 1 : 0;
            }

            string ret = Processing(vixCentralRec, expDates, liveDate, liveFuturesDataTime, spotVixValue, titleGAS, dataSourceGAS);

            return ret;

        }

        private string Processing(VixCentralRec2[] p_vixCentralRec, DateTime[] p_expDates, string p_liveDate, string p_liveFuturesDataTime, double p_spotVixValue, string p_titleF, string p_dataSource)
        {
            //Calculating dates to html.           
            DateTime timeNowET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            
            //Creating the current data array (prices and spreads).
            double[] currData = new double[28];
            currData[0] = p_vixCentralRec[0].F1;
            currData[1] = p_vixCentralRec[0].F2;
            currData[2] = p_vixCentralRec[0].F3;
            currData[3] = p_vixCentralRec[0].F4;
            currData[4] = p_vixCentralRec[0].F5;
            currData[5] = p_vixCentralRec[0].F6;
            currData[6] = p_vixCentralRec[0].F7;
            currData[7] = p_vixCentralRec[0].F8;
            currData[8] = p_vixCentralRec[0].STCont;
            currData[9] = p_vixCentralRec[0].LTCont;
            currData[10] = p_vixCentralRec[0].F2 - p_vixCentralRec[0].F1;
            currData[11] = p_vixCentralRec[0].F3 - p_vixCentralRec[0].F2;
            currData[12] = p_vixCentralRec[0].F4 - p_vixCentralRec[0].F3;
            currData[13] = p_vixCentralRec[0].F5 - p_vixCentralRec[0].F4;
            currData[14] = p_vixCentralRec[0].F6 - p_vixCentralRec[0].F5;
            currData[15] = p_vixCentralRec[0].F7 - p_vixCentralRec[0].F6;
            currData[16] = (p_vixCentralRec[0].F8 > 0) ? p_vixCentralRec[0].F8 - p_vixCentralRec[0].F7:0;
            currData[17] = p_vixCentralRec[0].F7 - p_vixCentralRec[0].F4;
            currData[18] = (p_vixCentralRec[0].F7 - p_vixCentralRec[0].F4)/3;
            currData[19] = p_vixCentralRec[0].F2 / p_vixCentralRec[0].F1 -1;
            currData[20] = p_vixCentralRec[0].F3 / p_vixCentralRec[0].F2 -1;
            currData[21] = p_vixCentralRec[0].F4 / p_vixCentralRec[0].F3 -1;
            currData[22] = p_vixCentralRec[0].F5 / p_vixCentralRec[0].F4 -1;
            currData[23] = p_vixCentralRec[0].F6 / p_vixCentralRec[0].F5 -1;
            currData[24] = p_vixCentralRec[0].F7 / p_vixCentralRec[0].F6 -1;
            currData[25] = (p_vixCentralRec[0].F8 > 0) ? p_vixCentralRec[0].F8 / p_vixCentralRec[0].F7 -1: 0;
            currData[26] = p_vixCentralRec[0].F7 / p_vixCentralRec[0].F4 -1;
            currData[27] = (p_vixCentralRec[0].F7 / p_vixCentralRec[0].F4 -1) / 3;

            //Creating the current days to expirations array.
            double[] currDataDays = new double[17];
            currDataDays[0] = p_vixCentralRec[0].F1expDays;
            currDataDays[1] = p_vixCentralRec[0].F2expDays;
            currDataDays[2] = p_vixCentralRec[0].F3expDays;
            currDataDays[3] = p_vixCentralRec[0].F4expDays;
            currDataDays[4] = p_vixCentralRec[0].F5expDays;
            currDataDays[5] = p_vixCentralRec[0].F6expDays;
            currDataDays[6] = p_vixCentralRec[0].F7expDays;
            currDataDays[7] = (p_vixCentralRec[0].F8>0)? p_vixCentralRec[0].F8expDays:0;
            currDataDays[8] = p_vixCentralRec[0].F1expDays;
            currDataDays[9] = p_vixCentralRec[0].F4expDays;
            currDataDays[10] = p_vixCentralRec[0].F1expDays;
            currDataDays[11] = p_vixCentralRec[0].F2expDays;
            currDataDays[12] = p_vixCentralRec[0].F3expDays;
            currDataDays[13] = p_vixCentralRec[0].F4expDays;
            currDataDays[14] = p_vixCentralRec[0].F5expDays;
            currDataDays[15] = p_vixCentralRec[0].F6expDays;
            currDataDays[16] = (p_vixCentralRec[0].F8 > 0) ? p_vixCentralRec[0].F7expDays:0;

            //Creating the data array of previous day (prices and spreads).
            double[] prevData = new double[17];
            prevData[0] = (p_vixCentralRec[0].F1expDays- p_vixCentralRec[1].F1expDays <=0) ?p_vixCentralRec[1].F1: p_vixCentralRec[1].F2;
            prevData[1] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F2 : p_vixCentralRec[1].F3;
            prevData[2] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F3 : p_vixCentralRec[1].F4;
            prevData[3] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F4 : p_vixCentralRec[1].F5;
            prevData[4] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F5 : p_vixCentralRec[1].F6;
            prevData[5] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F6 : p_vixCentralRec[1].F7;
            prevData[6] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F7 : p_vixCentralRec[1].F8;
            prevData[7] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? ((p_vixCentralRec[0].F8 > 0)? p_vixCentralRec[1].F8 :0 ): 0;
            prevData[8] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].STCont : p_vixCentralRec[1].F3/p_vixCentralRec[1].F2-1; 
            prevData[9] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].LTCont : p_vixCentralRec[1].F8 / p_vixCentralRec[1].F5 - 1; 
            prevData[10] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F2 - p_vixCentralRec[1].F1 : p_vixCentralRec[1].F3 - p_vixCentralRec[1].F2; 
            prevData[11] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F3 - p_vixCentralRec[1].F2 : p_vixCentralRec[1].F4 - p_vixCentralRec[1].F3;
            prevData[12] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F4 - p_vixCentralRec[1].F3 : p_vixCentralRec[1].F5 - p_vixCentralRec[1].F4;
            prevData[13] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F5 - p_vixCentralRec[1].F4 : p_vixCentralRec[1].F6 - p_vixCentralRec[1].F5;
            prevData[14] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F6 - p_vixCentralRec[1].F5 : p_vixCentralRec[1].F7 - p_vixCentralRec[1].F6;
            prevData[15] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? p_vixCentralRec[1].F7 - p_vixCentralRec[1].F6 : p_vixCentralRec[1].F8 - p_vixCentralRec[1].F7;
            prevData[16] = (p_vixCentralRec[0].F1expDays - p_vixCentralRec[1].F1expDays <= 0) ? ((p_vixCentralRec[0].F8 > 0) ? p_vixCentralRec[1].F8 - p_vixCentralRec[1].F7 :0) : 0;

            //Creating the difference of current and previous data array (prices and spreads).
            double[] currDataDiff = new double[17];
            for (int iRow = 0; iRow < currDataDiff.Length; iRow++)
            {
                currDataDiff[iRow] = currData[iRow] - prevData[iRow];
            }

            //Creating the %change of current and previous data array (prices and spreads).
            double[] currDataPercCh = new double[17];
            for (int iRow = 0; iRow < currDataPercCh.Length; iRow++)
            {
                currDataPercCh[iRow] = (prevData[iRow]==0)?0:(currData[iRow]/prevData[iRow]-1);
            }


            //Creating input string for JavaScript.
            StringBuilder sb = new StringBuilder("{" + Environment.NewLine);

            sb.Append(@"""dataSource"": """+ p_dataSource);

            sb.Append(@"""," + Environment.NewLine + @"""titleCont"": """ + p_titleF);

            sb.Append(@"""," + Environment.NewLine + @"""timeNow"": """ + timeNowET.ToString("yyyy-MM-dd HH:mm") + " EST");

            sb.Append(@"""," + Environment.NewLine + @"""liveDataDate"": """ + p_liveDate);

            sb.Append(@"""," + Environment.NewLine + @"""liveDataTime"": """ + p_liveFuturesDataTime);

            sb.Append(@"""," + Environment.NewLine + @"""currDataVec"": """);
            for (int i = 0; i < currData.Length - 1; i++)
                sb.Append(Math.Round(currData[i], 4).ToString() + ", ");
            sb.Append(Math.Round(currData[currData.Length - 1], 4).ToString());

            sb.Append(@"""," + Environment.NewLine + @"""currDataDaysVec"": """);
            for (int i = 0; i < currDataDays.Length - 1; i++)
                sb.Append(currDataDays[i].ToString() + ", ");
            sb.Append(currDataDays[currDataDays.Length - 1].ToString());

            sb.Append(@"""," + Environment.NewLine + @"""prevDataVec"": """);
            for (int i = 0; i < prevData.Length - 1; i++)
                sb.Append(Math.Round(prevData[i], 4).ToString() + ", ");
            sb.Append(Math.Round(prevData[prevData.Length - 1], 4).ToString());

            sb.Append(@"""," + Environment.NewLine + @"""currDataDiffVec"": """);
            for (int i = 0; i < currDataDiff.Length - 1; i++)
                sb.Append(Math.Round(currDataDiff[i], 4).ToString() + ", ");
            sb.Append(Math.Round(currDataDiff[currDataDiff.Length - 1], 4).ToString());

            sb.Append(@"""," + Environment.NewLine + @"""currDataPercChVec"": """);
            for (int i = 0; i < currDataPercCh.Length - 1; i++)
                sb.Append(Math.Round(currDataPercCh[i], 4).ToString() + ", ");
            sb.Append(Math.Round(currDataPercCh[currDataPercCh.Length - 1], 4).ToString());

            sb.Append(@"""," + Environment.NewLine + @"""spotVixVec"": """);
            for (int i = 0; i < currData.Length - 1; i++)
                sb.Append(Math.Round(p_spotVixValue, 4).ToString() + ", ");
            sb.Append(Math.Round(p_spotVixValue, 4).ToString());

            sb.AppendLine(@"]"""+ Environment.NewLine + @"}");
           
            return sb.ToString();
                   
        }
    }
}





    // [ApiController]
    // [Route("[controller]")]
    // [ResponseCache(CacheProfileName = "DefaultMidDuration")]
    // public class ContangoVisualizerController : ControllerBase
    // {
    //     public class ExampleMessage
    //     {
    //         public string MsgType { get; set; } = string.Empty;

    //         public string StringData { get; set; } = string.Empty;
    //         public DateTime DateOrTime { get; set; }

    //         public int IntData { get; set; }

    //         public int IntDataFunction => 32 + (int)(IntData / 0.5556);
    //     }
    //     private readonly ILogger<WeatherForecastController> _logger;

    //     public ContangoVisualizerController(ILogger<WeatherForecastController> logger)
    //     {
    //         _logger = logger;
    //     }

    //     [HttpGet]
    //     public IEnumerable<ExampleMessage> Get()
    //     {
    //         Thread.Sleep(5000);     // intentional delay to simulate a longer process to crunch data. This can be removed.

    //         var userEmailClaim = HttpContext?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
    //         string email = userEmailClaim?.Value  ?? "Unknown email";

    //         var firstMsgToSend = new ExampleMessage
    //         {
    //             MsgType = "AdminMsg",
    //             StringData = $"Cookies says your email is '{email}'.",
    //             DateOrTime = DateTime.Now,
    //             IntData = 0,                
    //         };

    //         string[] RandomStringDataToSend = new[]  { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
    //         var rng = new Random();
    //         return (new ExampleMessage[] { firstMsgToSend }.Concat(Enumerable.Range(1, 5).Select(index => new ExampleMessage
    //         {
    //             MsgType = "Msg-type",
    //             StringData = RandomStringDataToSend[rng.Next(RandomStringDataToSend.Length)],
    //             DateOrTime = DateTime.Now.AddDays(index),
    //             IntData = rng.Next(-20, 55)                
    //         }))).ToArray();
    //     }
    // }