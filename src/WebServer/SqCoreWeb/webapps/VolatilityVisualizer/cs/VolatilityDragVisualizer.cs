using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SqCommon;
using FinTechCommon;
using System.Text;

namespace SqCoreWeb.Controllers
{    
    [ApiController]
    [Route("[controller]")]
    [ResponseCache(CacheProfileName = "NoCache")]
    public class VolatilityDragVisualizerController : ControllerBase
    {
        public class ExampleMessage
        {
            public string MsgType { get; set; } = string.Empty;

            public string StringData { get; set; } = string.Empty;
            public DateTime DateOrTime { get; set; }

            public int IntData { get; set; }

            public int IntDataFunction => 32 + (int)(IntData / 0.5556);
        }

        public VolatilityDragVisualizerController()
        {
        }

        public class DailyData
        {
            public DateTime Date { get; set; }
            public double AdjClosePrice { get; set; }
        }

        [HttpGet]
        public string Get() 
        {
            Thread.Sleep(1000);     // intentional delay to simulate a longer process to crunch data. This can be removed.
            string mockupTestResponse = @"{
                ""requestTime"": ""Request time (UTC): 2022-06-03 12:48:42"",
                ""lastDataTime"": ""Last data time (UTC): Live data at 2022-06-03 12:48:42"",
                ""volLBPeri"": ""20"",
                ""retHistLBPeri"": ""20"",
                ""retLBPeris"": ""1 Day, 3 Days, 1 Week, 2 Weeks, 1 Month, 3 Months, 6 Months, 1 Year"",
                ""retLBPerisNo"": ""1, 3, 5, 10, 20, 63, 126, 252"",
                ""assetNames"": ""SPY, UPRO, QQQ, TQQQ, TLT, TMV, UCO, SO, UNG, SVXY, VXX, VXZ, AAPL, ADBE, AMZN, CRM, FB, GOOGL, MA, MSFT, NOW, NVDA, PYPL, QCOM, SQ, V, MDY, ILF, FEZ, EEM, EPP, VNQ"",
                ""defCheckedList"": ""SPY, QQQ, TQQQ, VXX, TMV, UCO, UNG"",
                ""volAssetNames"": ""SVXY, VXX, VXZ"",
                ""etpAssetNames"": ""SPY, UPRO, QQQ, TQQQ, TLT, TMV, UCO, SO, UNG"",
                ""gchAssetNames"": ""AAPL, ADBE, AMZN, CRM, FB, GOOGL, MA, MSFT, NOW, NVDA, PYPL, QCOM, SQ, V"",
                ""gmAssetNames"": ""MDY, ILF, FEZ, EEM, EPP, VNQ"",
                ""quotesDateVector"": ""2022-01-03, 2022-01-04, 2022-01-05, 2022-01-06, 2022-01-07, 2022-01-10, 2022-01-11, 2022-01-12, 2022-01-13, 2022-01-14, 2022-01-18, 2022-01-19, 2022-01-20, 2022-01-21, 2022-01-24, 2022-01-25, 2022-01-26, 2022-01-27, 2022-01-28, 2022-01-31, 2022-02-01, 2022-02-02, 2022-02-03, 2022-02-04, 2022-02-07, 2022-02-08, 2022-02-09, 2022-02-10, 2022-02-11, 2022-02-14, 2022-02-15, 2022-02-16, 2022-02-17, 2022-02-18, 2022-02-22, 2022-02-23, 2022-02-24, 2022-02-25, 2022-02-28, 2022-03-01, 2022-03-02, 2022-03-03, 2022-03-04, 2022-03-07, 2022-03-08, 2022-03-09, 2022-03-10, 2022-03-11, 2022-03-14, 2022-03-15, 2022-03-16, 2022-03-17, 2022-03-18, 2022-03-21, 2022-03-22, 2022-03-23, 2022-03-24, 2022-03-25, 2022-03-28, 2022-03-29, 2022-03-30, 2022-03-31, 2022-04-01, 2022-04-04, 2022-04-05, 2022-04-06, 2022-04-07, 2022-04-08, 2022-04-11, 2022-04-12, 2022-04-13, 2022-04-14, 2022-04-18, 2022-04-19, 2022-04-20, 2022-04-21, 2022-04-22, 2022-04-25, 2022-04-26, 2022-04-27, 2022-04-28, 2022-04-29, 2022-05-02, 2022-05-03, 2022-05-04, 2022-05-05, 2022-05-06, 2022-05-09, 2022-05-10, 2022-05-11, 2022-05-12, 2022-05-13, 2022-05-16, 2022-05-17, 2022-05-18, 2022-05-19, 2022-05-20, 2022-05-23, 2022-05-24, 2022-05-25, 2022-05-26, 2022-05-27, 2022-05-31, 2022-06-01, 2022-06-02, 2022-06-03"",
                ""dailyVolDrags"": ""0.25%, 2.21%, 0.48%, 4.24%, 0.21%, 1.89%, 5.67%, 0.24%, 1.21%, 0.84%, 3.7%, 0.54%, 0.39%, 1.07%, 0.93%, 0.95%, 1.42%, 0.54%, 0.53%, 0.55%, 1.65%, 2.16%, 1.64%, 1.24%, 7.33%, 0.44%, 0.33%, 0.28%, 0.87%, 0.46%, 0.19%, 0.17%ß 0.15%, 1.37%, 0.35%, 3.07%, 0.14%, 1.29%, 3.99%, 0.18%, 1.19%, 0.54%, 1.17%, 0.27%, 0.33%, 0.9%, 0.59%, 0.71%, 0.95%, 0.4%, 0.4%, 0.42%, 1.17%, 1.66%, 1.2%, 1.01%, 3.19%, 0.36%, 0.18%, 0.22%, 0.32%, 0.31%, 0.14%, 0.15%ß 0.42%, 3.7%, 0.74%, 6.44%, 0.17%, 1.52%, 2.47%, 0.18%, 2.97%, 1.26%, 1.88%, 0.74%, 0.87%, 0.87%, 1.94%, 1.15%, 2.89%, 0.81%, 0.82%, 0.79%, 1.54%, 2.54%, 2.09%, 1.39%, 4.45%, 0.81%, 0.42%, 0.42%, 0.33%, 0.25%, 0.25%, 0.38%ß 0.39%, 3.42%, 0.7%, 6.2%, 0.19%, 1.69%, 2.15%, 0.08%, 2.71%, 0.9%, 1.09%, 0.63%, 0.96%, 0.96%, 1.43%, 1.47%, 1.29%, 0.69%, 0.57%, 0.55%, 1.65%, 2.74%, 1.5%, 0.97%, 5.93%, 0.59%, 0.45%, 0.39%, 0.4%, 0.31%, 0.27%, 0.34%"",
                ""yearlyVIXAvgs"": ""15.35, 12.84, 12.82, 17.15, 31.73, 32.46, 22.68, 23.96, 18.01, 14.38, 14.06, 16.63, 16.04, 11.18, 16, 15.9, 28.89, 19.8, 25.24"",
                ""monthlyVIXAvgs"": ""17.07, 16.06, 16.83, 16.49, 15.23, 16.51, 15, 14.32, 14.64, 12.87, 12.86, 12.53, 12.34, 13.78, 14.69, 12.61, 11.49, 11.92, 12.97, 13.78, 13.74, 11.45, 11.43, 12.59, 12, 11.66, 12.7, 16.77, 15.64, 14.61, 12.47, 11.72, 10.93, 10.92, 11.05, 10.7, 14.01, 13.67, 13.09, 14.01, 15.79, 22.42, 24.35, 19.44, 22.96, 23.85, 23.15, 26.55, 26.03, 24.25, 19.45, 20.14, 23.94, 22.14, 23.94, 47.97, 63.52, 60.01, 45.33, 45.18, 46.15, 40.72, 34.8, 30.34, 27.66, 25.19, 25.32, 24.41, 24.42, 22.11, 20.09, 22.6, 19.53, 17.06, 24.47, 32.02, 27.68, 24.09, 24.03, 21.51, 19.91, 19, 17.12, 17.17, 19.84, 18.38, 16.42, 18.06, 18.68, 28.53, 35.75, 36.23, 31.25, 28.84, 22.14, 18.97, 17.46, 16.66, 19.05, 22.34, 18.44, 16.5, 15.7, 15.41, 17.1, 16.37, 15.57, 13.46, 13.81, 13.45, 13.59, 15.51, 15.85, 13.37, 15.08, 15.58, 13.46, 13.77, 13.61, 15.79, 14.73, 14.54, 13.38, 11.8, 11.67, 13.76, 12.67, 16.67, 15.51, 14.69, 18.15, 17.89, 14.88, 14.25, 13.33, 13.71, 15, 14.56, 24.45, 20.37, 15.76, 17.34, 20.81, 23.45, 18.87, 14.6, 14.66, 15.79, 16.27, 12.39, 13.57, 14.22, 15.89, 12.97, 12.03, 11.45, 11.74, 12.75, 11.66, 10.65, 10.6, 11.06, 11.42, 9.98, 10.43, 10.52, 10.23, 18.01, 19.48, 19.57, 15.43, 13.35, 14.01, 12.56, 13, 15.5, 20.47, 21.22, 23.36, 17.05, 14.71, 13.7, 14.92, 16.52, 14.23, 16.68, 17.1, 16.16, 13.17, 13.36, 13.27, 15.6, 40.2, 52.91, 35, 29.75, 29.87, 24.09, 25.84, 27.45, 29.18, 22.39, 23.39, 24.43, 23.08, 18.64, 18.86, 18.15, 16.95, 17.78, 18.26, 19.86, 16.84, 21.87, 19.92, 25.06, 28.69, 22.75, 28.54, 29"",
                ""yearlyCounts"": ""194, 252, 251, 251, 253, 252, 252, 252, 250, 252, 252, 252, 252, 251, 251, 252, 253, 252, 106"",
                ""noTotalDays"": ""4580"",
                ""monthlyCounts"": ""4, 21, 20, 21, 21, 22, 21, 21, 21, 22, 20, 19, 22, 21, 21, 22, 20, 23, 21, 21, 21, 21, 20, 19, 23, 19, 22, 22, 20, 23, 20, 22, 21, 20, 20, 19, 22, 20, 22, 21, 21, 23, 19, 23, 21, 20, 21, 20, 20, 22, 21, 21, 22, 21, 21, 23, 19, 22, 20, 19, 22, 21, 20, 22, 22, 21, 21, 22, 20, 22, 19, 19, 23, 21, 20, 22, 21, 22, 21, 21, 21, 22, 20, 19, 23, 20, 21, 22, 20, 23, 21, 21, 21, 21, 20, 20, 22, 20, 22, 21, 21, 23, 19, 21, 21, 20, 21, 19, 20, 22, 22, 20, 22, 22, 20, 23, 20, 21, 21, 19, 21, 21, 21, 21, 22, 21, 21, 23, 19, 22, 20, 19, 22, 21, 20, 22, 22, 21, 21, 22, 20, 22, 19, 20, 22, 21, 21, 22, 20, 23, 21, 21, 21, 21, 20, 19, 23, 19, 22, 22, 20, 23, 20, 22, 21, 20, 21, 19, 21, 21, 22, 21, 21, 23, 19, 23, 21, 19, 21, 19, 21, 21, 22, 20, 22, 22, 20, 23, 20, 21, 21, 19, 22, 21, 20, 22, 22, 21, 21, 22, 20, 22, 19, 19, 23, 21, 20, 22, 21, 22, 21, 21, 21, 22, 20, 19, 23, 20, 21, 3"",
                ""vixAvgTotal"": ""19.08"",
                ""volDragsAvgsTotalVec"": ""0.16%, 1.4%, 0.19%, 1.7%, 0.08%, 0.84%, 2.2%, 0.16%, 0.84%, 0.47%, 1.68%, 0.41%, 0.46%, 0.48%, 0.62%, 0.76%, 0.62%, 0.38%, 0.5%, 0.31%, 0.71%, 0.99%, 0.58%, 0.47%, 1.37%, 0.39%, 0.21%, 0.47%, 0.29%, 0.36%, 0.26%, 0.39%"",
                ""histRetMtx"": ""-0.89%, -2.49%, -1.35%, -3.98%, -1.08%, 3.17%, 0.38%, 0%, 1.63%, -0.65%, 1.61%, 0%, -2.45%, -1.42%, -0.65%, -1.75%, -1.34%, -1.28%, 0%, -1.38%, -1.46%, -2%, -1.89%, -1.92%, -2.18%, -0.72%, -0.46%, 0%, -0.79%, -0.98%, 0%, -0.87%ß 0.18%, 0.52%, 0.6%, 1.39%, -1.32%, 3.57%, 4.58%, 0.38%, 4.84%, 0.7%, -2.26%, -1.84%, -0.9%, 4.45%, 3.74%, 15.52%, 1.32%, 2.07%, 1.46%, -0.39%, 6.41%, 2.83%, 1.69%, 0.65%, -2.26%, 0.63%, 0.96%, 0.28%, 0.23%, 0.02%, 2.01%, -0.34%ß 2.07%, 6.04%, 3.61%, 10.52%, -3.17%, 9.31%, 6.47%, 0.88%, -1.58%, 3.66%, -5.2%, -3.84%, 2.59%, 6.46%, 12.26%, 13.94%, 2.38%, 7.72%, 3.77%, 1.84%, 9.36%, 7.56%, 7.75%, 6.91%, 2.54%, 2.37%, 2.27%, 0.39%, 1.6%, 2.6%, 3.37%, 1.07%ß 6.22%, 19.23%, 7.1%, 21.5%, -1.84%, 4.29%, 17.32%, 3.57%, 4.57%, 5.53%, -13.03%, -4.68%, 7.39%, 10.3%, 16.2%, 18.97%, 2.57%, 5.2%, 9.29%, 6.98%, 16.56%, 12.13%, 6.61%, 10.4%, -1.85%, 8.17%, 6.19%, 7.06%, 6.08%, 4.1%, 5.21%, 4.78%ß -0.03%, -2.2%, -0.92%, -6.55%, -0.3%, -1.1%, 15.06%, 3.5%, -2.79%, 6.85%, -18.33%, -5.45%, -5.77%, 8.61%, 7.12%, 7.43%, -5.8%, -0.33%, 2.75%, -2.13%, 5.99%, 1.89%, 1.43%, 1.78%, -10.49%, 4.22%, 1.13%, 11.41%, 5.83%, 2.77%, 3.53%, -2.34%ß -3.98%, -16.64%, -7.94%, -30.32%, -17.72%, 68.52%, 12.73%, 13.43%, 68.74%, 3.24%, -15.84%, 0.71%, -9.47%, -3.79%, -14.38%, -8.82%, -1.93%, -11.97%, 9.92%, -6.36%, -9.07%, -16.28%, -13.27%, -11.16%, -19.71%, 6.8%, -2.61%, 3.56%, 6.47%, -4.42%, 1.5%, -5.67%ß -8.96%, -31.27%, -20.26%, -57.59%, -24.06%, 103.89%, 186.35%, 24.89%, 117.38%, -5.58%, -14.27%, 2.32%, -9.68%, -35.26%, -27.44%, -29.13%, -36.79%, -18.78%, 13.7%, -17.45%, -20.2%, -40.22%, -53.7%, -18.23%, -55.49%, 8.05%, -6.77%, 23.87%, -10.43%, -12.77%, 1.68%, -7.33%ß -0.82%, -13.66%, -7.15%, -35.55%, -16.57%, 42.85%, 182.51%, 22.85%, 168.69%, -4.57%, -33.96%, 5.6%, 17.84%, -13.78%, -22.21%, -22.05%, -40.61%, -2.97%, -0.29%, 8.86%, 7.98%, 9.31%, -67.06%, 8.66%, -59.97%, -6.61%, -6.14%, -4.52%, -15.94%, -22.36%, -8.57%, -0.97%"",
                ""histRet2Chart"": ""7.3%, 14.4%, 4.54%, -0.29%, -9.4%, 16.26%, 123.95%, 22.59%, 102.85%, 3.86%, -26.12%, 0.82%, 35.68%, -11.73%, -3.65%, -16.97%, -32.7%, 11.37%, -3.26%, 19.1%, 11.93%, 31.61%, -57.72%, 3.04%, -42.34%, -5.27%, -3.43%, 0.01%, -15.73%, -18.2%, -4.37%, 10.11%ß 5.47%, 8.42%, 2.06%, -7.37%, -10.84%, 21.64%, 115.7%, 19.37%, 113.96%, 0.95%, -21.83%, 2.84%, 32.22%, -13.89%, -5.73%, -17.7%, -34.48%, 7.64%, -4.61%, 14.41%, 11.02%, 24.77%, -58.25%, 2.04%, -42.4%, -6.24%, -3.88%, -0.4%, -16.29%, -19.22%, -5.7%, 8.93%ß 5.08%, 7.17%, 1.63%, -8.56%, -10.96%, 22.03%, 132.09%, 21.07%, 114.88%, 0.32%, -22.91%, 3.66%, 33.74%, -15.41%, -5.94%, -17.97%, -35.18%, 6.71%, -5.04%, 13.12%, 10.76%, 22.42%, -58.85%, 2.59%, -42.49%, -7.71%, -4.02%, -0.9%, -16.84%, -19.48%, -5.74%, 8.77%ß 6.28%, 10.83%, 3.7%, -3.08%, -10.78%, 20.99%, 144.49%, 20.78%, 126.08%, 3.85%, -26.36%, 1.64%, 35.93%, -14.44%, -2.98%, -17.58%, -34.92%, 8.54%, -2.47%, 15.35%, 14.2%, 26.4%, -60.02%, 5.91%, -41.01%, -6.89%, -2.46%, -0.4%, -15.87%, -18.42%, -5.22%, 9.54%ß 4.96%, 6.76%, 1.33%, -9.6%, -12.57%, 28.67%, 148.53%, 21.79%, 135.08%, 2.46%, -25.16%, 2.88%, 31.85%, -16.66%, -5.37%, -20.24%, -36.38%, 5.89%, -1.74%, 12.23%, 10.13%, 21.02%, -61.1%, 3.2%, -43.23%, -7.1%, -3.03%, -1.74%, -16.36%, -19.48%, -5.55%, 9.07%ß 5%, 6.8%, 1.4%, -9.35%, -13%, 30.74%, 151.95%, 21.53%, 149.13%, 3.13%, -24.2%, 2.95%, 31.68%, -15.67%, -4.69%, -21.25%, -36.2%, 6.68%, -2.02%, 12.51%, 8.01%, 24.01%, -61.76%, 5.02%, -44.13%, -6.93%, -3.22%, -1.13%, -16.46%, -19.75%, -6.13%, 8.63%ß 6.7%, 11.92%, 3.67%, -3.52%, -13.65%, 33.31%, 129.9%, 22.24%, 129.48%, 5.74%, -25.94%, 0.9%, 33.54%, -13.5%, -1.37%, -19.41%, -34.22%, 8.63%, -0.08%, 14.42%, 11.67%, 26.37%, -60.59%, 6.32%, -41.21%, -5.83%, -1.03%, -1.53%, -15.87%, -20.15%, -5.16%, 10.99%ß 6.62%, 11.66%, 2.16%, -7.87%, -11.91%, 25.39%, 132.48%, 22.61%, 120.39%, 6.97%, -26.95%, -0.49%, 33.4%, -15.14%, -3.94%, -21.58%, -39.33%, 6.99%, 0.31%, 14.85%, 9.68%, 22.29%, -63.92%, 5.79%, -46.41%, -4.92%, -0.27%, -1.74%, -14.65%, -20.63%, -4.93%, 12.84%ß 5.02%, 6.69%, 0.04%, -13.27%, -12.57%, 28.36%, 138.03%, 22.54%, 122.22%, 2.99%, -25.55%, 2.43%, 32.75%, -17.25%, -7.49%, -25.37%, -43.07%, 4.29%, 0.08%, 12.62%, 6.73%, 14.9%, -66.01%, 2.6%, -49.6%, -5.5%, -2.07%, -4.75%, -15.45%, -22.15%, -6.51%, 12.06%ß 2.14%, -2.01%, -2.57%, -20.16%, -13.12%, 30.59%, 125.95%, 21.66%, 108.45%, -3.16%, -21.68%, 6.2%, 29.06%, -19%, -9.96%, -27.81%, -44.27%, -0.04%, -3.56%, 9.9%, 2.33%, 11.1%, -67.29%, 0.11%, -51.95%, -9.12%, -4.71%, -7.5%, -16.78%, -22.85%, -9.05%, 10.09%ß 2.73%, -0.43%, -1.32%, -17.2%, -12.25%, 26.58%, 118.07%, 20.15%, 122.41%, -2.33%, -25.73%, 4.85%, 29.93%, -17.95%, -8.88%, -26.49%, -43.4%, 2.84%, -2.62%, 12.59%, 2.52%, 13.3%, -66.63%, 2.94%, -49.75%, -8.34%, -4.14%, -9.47%, -17.6%, -23.41%, -10.21%, 9.81%ß -0.24%, -9.03%, -5.05%, -26.51%, -11.36%, 23.09%, 125.8%, 18.18%, 118.73%, -7.58%, -21.08%, 8.89%, 25.08%, -20.89%, -13.05%, -28.38%, -45.22%, -0.86%, -5.5%, 8.37%, -1.01%, 6.96%, -68.18%, 0.63%, -52.98%, -12.2%, -6.99%, -12.62%, -20.44%, -25.09%, -11.82%, 8%ß 0.04%, -8.37%, -5.16%, -26.83%, -12.5%, 27.32%, 124.95%, 17.77%, 131.31%, -8.35%, -18.54%, 9.07%, 24.9%, -21.13%, -13.81%, -26.44%, -47.04%, -4.5%, -0.71%, 13.59%, 1.22%, 4.83%, -68.59%, 1.84%, -53.24%, -6.52%, -6.87%, -11.88%, -20.42%, -24.21%, -11.03%, 7.29%ß 2.57%, -1.43%, -1.79%, -19.1%, -12.37%, 27.19%, 131.94%, 21.13%, 119.01%, -6%, -22.01%, 10.04%, 30.54%, -18.63%, -9.8%, -21.79%, -37.72%, -0.97%, 4.03%, 16.16%, 9.41%, 12.62%, -64.99%, 11.71%, -51.03%, -3.66%, -5.11%, -10.95%, -18.88%, -23.12%, -9.94%, 9.27%ß -1.22%, -12.23%, -6.21%, -29.74%, -13.51%, 31.97%, 126.29%, 17.62%, 129.57%, -9.72%, -17.01%, 10.42%, 25.76%, -21.52%, -22.47%, -25.91%, -39.32%, -4.65%, -0.21%, 11.3%, 3.79%, 5.58%, -66.57%, 5.3%, -53.42%, -6.95%, -7.72%, -12.59%, -20.1%, -22.85%, -11.05%, 4.25%ß -0.63%, -10.7%, -4.65%, -26.43%, -15.01%, 38.74%, 133.63%, 17.33%, 138.75%, -9.04%, -17.64%, 10.19%, 26.01%, -19.27%, -22.34%, -25.23%, -36.09%, -2.59%, -1.4%, 14.09%, 5.18%, 11.2%, -65.2%, 9.51%, -50.46%, -7.65%, -7.15%, -14.43%, -20.12%, -22.96%, -11.14%, 1.73%ß -0.17%, -9.5%, -4.54%, -26.26%, -14.43%, 35.97%, 125.54%, 17.19%, 146.01%, -6.35%, -18.99%, 7.99%, 27.22%, -19.21%, -22.49%, -24.89%, -35.82%, -1.96%, -1.74%, 13.01%, 4.6%, 11.59%, -65.51%, 8.74%, -52.11%, -8.95%, -6.25%, -13.22%, -19.49%, -22.39%, -10.29%, 3%ß 2.87%, -1.29%, -1.32%, -18.95%, -13.96%, 33.88%, 144.01%, 19.51%, 166.39%, -2.1%, -25.58%, 3.73%, 32.44%, -16.09%, -21.45%, -21.9%, -32.37%, 2.16%, 1.2%, 16.3%, 8.43%, 15.76%, -64.75%, 12.35%, -50.03%, -6.34%, -3.61%, -11.01%, -17.75%, -21.55%, -8.64%, 4.18%ß -0.79%, -11.72%, -6.29%, -31.03%, -16.32%, 44.44%, 145.54%, 18.7%, 176.4%, -10.68%, -19.14%, 11.69%, 25.06%, -20.61%, -27.39%, -27.45%, -36.95%, -2.65%, -2.96%, 11.23%, 1.88%, 7.28%, -67.52%, 6.77%, -55.29%, -10.4%, -7.19%, -14.29%, -20.57%, -24.45%, -11.68%, 1.4%ß -1.38%, -13.32%, -7.41%, -33.43%, -17.55%, 51.29%, 154.46%, 19.67%, 154.18%, -10.12%, -18.93%, 11.69%, 25.65%, -22.5%, -28.41%, -28.54%, -38.32%, -3.29%, -4.68%, 10.18%, -0.85%, 6.31%, -68.95%, 5.97%, -54.99%, -11.45%, -8.43%, -15.2%, -21.72%, -25.49%, -12.94%, 0.2%ß -4.54%, -21.65%, -11.03%, -41.3%, -16.83%, 47.33%, 124.16%, 18.97%, 123.23%, -13.79%, -16.11%, 15.87%, 21.48%, -25.29%, -32.14%, -31.11%, -40.61%, -5.99%, -9.93%, 6.11%, -7.55%, -3.51%, -69.77%, 1.7%, -60.76%, -15.74%, -11.49%, -18.15%, -23.89%, -27.63%, -15.44%, -4.21%ß -4.32%, -21.22%, -9.95%, -39.28%, -16.07%, 43.07%, 113.7%, 17.32%, 127.64%, -12.37%, -18.97%, 14.9%, 23.44%, -22.1%, -32.1%, -29.62%, -40.17%, -4.41%, -10.66%, 8.09%, -6.15%, 0.17%, -70.06%, 3.13%, -60.51%, -15.49%, -11.78%, -17.81%, -23.07%, -27.29%, -15.48%, -5.96%ß -5.84%, -24.96%, -12.62%, -44.68%, -14.46%, 35.1%, 132.57%, 18.57%, 142.24%, -12.16%, -18.3%, 14.19%, 17.04%, -24.81%, -34.27%, -32.09%, -42.87%, -5.08%, -10.51%, 4.5%, -8.75%, -5.33%, -71.42%, -0.27%, -66.67%, -14.11%, -13.3%, -16.91%, -23.18%, -27.8%, -15.87%, -6.16%ß -5.94%, -25.21%, -12.83%, -45.12%, -14.61%, 35.36%, 137.61%, 18.73%, 143.16%, -11.29%, -19.35%, 14.15%, 13.89%, -23%, -33.3%, -32.45%, -42.11%, -5.71%, -11.78%, 2.41%, -6.02%, -7.92%, -71.76%, -0.67%, -64.55%, -15.15%, -12.47%, -16.2%, -23.32%, -28.27%, -16.85%, -5.43%ß -3.69%, -19.87%, -9.6%, -39.16%, -15.88%, 41.91%, 147.88%, 19.13%, 141.23%, -8.49%, -22.22%, 10.38%, 17.52%, -19.63%, -29.48%, -29.72%, -39.88%, -3.03%, -8.61%, 4.73%, -1.74%, 0.8%, -70.03%, 1.71%, -60.62%, -12.85%, -10.17%, -13.96%, -20.97%, -26.29%, -14.22%, -2.93%ß -4.08%, -20.8%, -10.65%, -41.05%, -15.96%, 41.96%, 156.71%, 20.1%, 151.61%, -6.61%, -26.06%, 9.78%, 16.27%, -20.15%, -30.88%, -30.89%, -39.45%, -4.37%, -9.45%, 4.88%, -6.02%, -1.72%, -70.48%, 0.94%, -62.7%, -13.47%, -10.69%, -12.72%, -21.05%, -26.58%, -14.03%, -3.54%ß -2.11%, -16.06%, -8.33%, -36.6%, -16.98%, 47.34%, 145.15%, 19.94%, 161.16%, -5.44%, -25.76%, 8.96%, 19.23%, -18.8%, -28.03%, -31.06%, -38.67%, -2.68%, -6.94%, 7.01%, -6.03%, 3.48%, -69.68%, 5.3%, -60.32%, -10.77%, -8.09%, -10.31%, -18.78%, -24.78%, -12.4%, -2.23%ß -6.05%, -26.07%, -12.83%, -45.85%, -15.21%, 37.94%, 134.35%, 19.41%, 161.62%, -12.24%, -21.86%, 14.41%, 12.5%, -21.13%, -33.18%, -33.75%, -41.81%, -6.5%, -7.76%, 2.14%, -10.51%, -3.57%, -70.66%, -1.67%, -61.6%, -12.52%, -11.5%, -12.75%, -21.58%, -26.58%, -14.18%, -5.07%ß -6.63%, -27.58%, -13.3%, -46.95%, -15.01%, 36.97%, 140.8%, 18.61%, 156.93%, -9.57%, -24.06%, 10.79%, 9.73%, -21.83%, -33.06%, -34.48%, -42.09%, -7.77%, -8.77%, 1.76%, -7.36%, -2.52%, -69.1%, -1.58%, -59.22%, -13.67%, -11.61%, -10.81%, -20.76%, -25.41%, -13.1%, -5.49%ß -6.59%, -27.45%, -13.57%, -47.41%, -14.04%, 32.13%, 146.33%, 18.74%, 154.64%, -9.68%, -24.98%, 11.61%, 9.92%, -20.89%, -32.89%, -32.77%, -41.41%, -9%, -7.68%, 1.53%, -6%, -4.96%, -69.38%, -0.8%, -60.95%, -12.94%, -11.87%, -9.14%, -20.63%, -25.1%, -12.28%, -4.89%ß -4.84%, -23.35%, -12.13%, -44.82%, -15.46%, 38.67%, 149.21%, 19.94%, 176.12%, -9.04%, -27.94%, 11.28%, 14.33%, -19.37%, -32.91%, -32.49%, -40.6%, -6.84%, -4.32%, 4.78%, -6.63%, -3.8%, -69.14%, -0.41%, -60.97%, -9.21%, -10.9%, -6.76%, -18.59%, -24.54%, -11.61%, -3.78%ß -5.57%, -25.1%, -14%, -48.26%, -13.79%, 30.45%, 150.82%, 22.43%, 176.58%, -9.21%, -28.03%, 12.1%, 12.13%, -21.03%, -35.06%, -33.92%, -45.12%, -11.45%, -6.44%, 4.37%, -8.9%, -8.04%, -70.1%, -3.11%, -64.49%, -11.37%, -12%, -6.99%, -19.01%, -25.85%, -12.17%, -2.97%ß -4.73%, -23.09%, -12.8%, -46.18%, -13.45%, 28.92%, 153.43%, 22.43%, 180.35%, -8.26%, -29.17%, 10.04%, 12.26%, -20.22%, -33.4%, -32.77%, -44.35%, -11.59%, -5.32%, 5.53%, -1.81%, -3.36%, -69.54%, -1.08%, -63.52%, -10.84%, -10.31%, -6.66%, -18.8%, -25.49%, -12.01%, -2.15%ß -2.83%, -18.58%, -10.38%, -41.68%, -13.84%, 30.69%, 165.33%, 21.79%, 173%, -7.94%, -30.34%, 9.82%, 14.86%, -19.01%, -30.71%, -31.59%, -41.99%, -9.93%, -3.91%, 6.89%, -1.25%, 1.62%, -69.43%, 1.64%, -60.97%, -8.78%, -8.22%, -4.88%, -17.26%, -24.32%, -11.55%, -2.03%ß -0.44%, -12.57%, -7.46%, -35.97%, -13.63%, 29.93%, 170.3%, 23.05%, 171.63%, -6.08%, -32.73%, 7.99%, 19.54%, -15.12%, -28.17%, -30.48%, -40.93%, -6.15%, -1.75%, 9.84%, 3.4%, 7.09%, -67.61%, 5.35%, -57.64%, -6.88%, -6.1%, -3.48%, -15.68%, -23.45%, -9.71%, 0.6%ß -1%, -14.1%, -7.7%, -36.43%, -15.46%, 37.92%, 170.13%, 22.38%, 156.29%, -5.23%, -32.43%, 7.58%, 18.91%, -17.45%, -25.01%, -32.52%, -41.38%, -4.94%, -1.72%, 9.29%, 1.48%, 6.3%, -67.61%, 7.96%, -59.05%, -7.19%, -7.03%, -4.78%, -16.13%, -22.37%, -10.37%, -0.63%ß -1.8%, -16.13%, -8.38%, -37.91%, -15.7%, 38.59%, 170.87%, 22.21%, 173.55%, -4.91%, -33.33%, 7.21%, 18.8%, -17.11%, -24.1%, -25.86%, -42.9%, -4.84%, -1.95%, 9.51%, 3.14%, 4.29%, -68.64%, 6.1%, -61.67%, -8.17%, -7.81%, -5.52%, -17.51%, -22.92%, -10.33%, -1.46%ß 0.07%, -11.46%, -5.87%, -32.87%, -15.66%, 38.46%, 181.44%, 22.85%, 164.37%, -3.94%, -35.01%, 5.6%, 20.8%, -12.53%, -21.71%, -20.67%, -39.8%, -1.72%, -0.29%, 10.38%, 9.58%, 11.53%, -66.42%, 10.79%, -59.08%, -5.93%, -5.71%, -4.52%, -15.26%, -21.59%, -8.57%, -0.1%ß -0.82%, -13.66%, -7.15%, -35.55%, -16.57%, 42.85%, 182.51%, 22.85%, 168.69%, -4.57%, -33.96%, 5.6%, 17.84%, -13.78%, -22.21%, -22.05%, -40.61%, -2.97%, -0.29%, 8.86%, 7.98%, 9.31%, -67.06%, 8.66%, -59.97%, -6.61%, -6.14%, -4.52%, -15.94%, -22.36%, -8.57%, -0.97%""
                }";
            return mockupTestResponse;
        }

         //Downloading price data from SQL Server
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

             DateTime nowET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
             DateTime startIncLoc = nowET.AddDays(-50);
          
            List<List<DailyData>> volatilityTickersData = new();
            // List<DailyData> VIXDailyquotes = new();

            List<(Asset asset, List<AssetHistValue> values)> assetHistsAndEst = MemDb.gMemDb.GetSdaHistClosesAndLastEstValue(assets, startIncLoc, true).ToList();
            for (int i = 0; i < assetHistsAndEst.Count - 1; i++)
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

        // public string GetStr(int p_lbP)
        // {
        //     //Defining asset lists.
        //     string[] volAssetList = new string[] { "SVXY!Light0.5x.SQ", "VXX.SQ", "VXZ.SQ"};
        //     string[] volAssetListNN = new string[] { "SVXY", "VXX", "VXZ"};

        //     //string[] volAssetList = new string[] { "SVXY!Light0.5x.SQ", "VXX.SQ", "VXZ.SQ", "UVXY!Light1.5x.SQ", "TVIX!Better1.SQ" };
        //     //string[] volAssetListNN = new string[] { "SVXY_Light", "VXX", "VXZ", "UVXY_Light", "TVIX_Better" };

        //     string[] etpAssetList = new string[] { "SPY", "UPRO.SQ", "QQQ", "TQQQ.SQ", "TLT", "TMV", "USO", "UCO", "UNG"};
        //     string[] etpAssetListNN = new string[] { "SPY", "UPRO", "QQQ", "TQQQ", "TLT", "TMV", "USO", "UCO", "UNG"};

        //     //string[] etpAssetList = new string[] { "SPY", "UPRO.SQ", "QQQ", "TQQQ.SQ", "FAS.SQ", "TMV", "UGAZ", "UWT", "UGLD" };
        //     //string[] etpAssetListNN = new string[] { "SPY", "UPRO", "QQQ", "TQQQ", "FAS", "TMV", "UGAZ", "UWT", "UGLD" };

        //     // 2022-05: BABA has 2 days less prices then everything else. Just ignore BABA
        //     // string[] gchAssetList = new string[] { "AAPL", "ADBE", "AMZN", "BABA", "CRM", "FB", "GOOGL", "MA", "MSFT", "NOW", "NVDA", "PYPL", "QCOM", "SQ", "V" };
        //     // string[] gchAssetListNN = new string[] { "AAPL", "ADBE", "AMZN", "BABA", "CRM", "FB", "GOOGL", "MA", "MSFT", "NOW", "NVDA", "PYPL", "QCOM", "SQ", "V" };
        //     string[] gchAssetList = new string[] { "AAPL", "AMZN", "FB", "GOOGL", "MSFT", "NVDA" };
        //     string[] gchAssetListNN = new string[] { "AAPL", "AMZN", "FB", "GOOGL", "MSFT", "NVDA" };

        //     string[] gmAssetList = new string[] { "MDY", "ILF", "FEZ", "EEM", "EPP", "VNQ"};
        //     string[] gmAssetListNN = new string[] { "MDY", "ILF", "FEZ", "EEM", "EPP", "VNQ" };

        //     string[] vixAssetList = new string[] { "^VIX" };

        //     string[] defaultCheckedList = new string[] { "SPY", "QQQ", "TQQQ", "VXX", "TMV", "UCO", "UNG" }; 

        //     var allAssetList = etpAssetList.Union(volAssetList).Union(gchAssetList).Union(gmAssetList).Union(vixAssetList).ToArray();
        //     var usedAssetList = etpAssetListNN.Union(volAssetListNN).Union(gchAssetListNN).Union(gmAssetListNN).ToArray();


        //     // string[] allAssetList = new string[]{ "SPY", "QQQ", "SVXY!Light0.5x.SQ", "VXX.SQ", "VXZ.SQ", "UVXY!Light1.5x.SQ", "TQQQ.SQ", "^VIX" };
        //     // string[] usedAssetList = new string[] { "SPY", "QQQ", "SVXY_Light", "VXX", "VXZ", "UVXY_Light", "TQQQ"};
        //     // string[] defaultCheckedList = new string[] { "SPY", "QQQ", "VXX"};

        //     int volLBPeriod = p_lbP;
        //     int[] retLB = new int[] {1, 3, 5, 10, 20, 63, 126, 252};
        //     string[] retLBStr = new string[] { "1 Day", "3 Days", "1 Week", "2 Weeks", "1 Month", "3 Months", "6 Months", "1 Year" };
        //     int retHistLB = 20;

        //     //Collecting and splitting price data got from SQL Server
        //     IList<List<DailyData>> quotesData = GetVolatilityStockHistData(allAssetList);
        //     IList<List<DailyData>> quotesData1= new List<List<DailyData>>(quotesData);
        //     quotesData1.RemoveAt(allAssetList.Length-1);
            
        //     List<DailyData> quotesData2 = quotesData[allAssetList.Length-1];


        //     int noAssets = allAssetList.Length-1;
        //     int noBtDays = quotesData1[0].Count;
        //     DateTime[] quotesDateVec = new DateTime[noBtDays];

        //     for (int iRows=0; iRows<quotesDateVec.Length;iRows++)
        //     {
        //         quotesDateVec[iRows] = quotesData1[0][iRows].Date;
        //     }

        //     DateTime[] quotesFirstDates = new DateTime[noAssets];

        //     for (int jAssets = 0; jAssets < quotesFirstDates.Length; jAssets++)
        //     {
        //         quotesFirstDates[jAssets] = quotesData1[jAssets][0].Date;
        //     }

        //     DateTime[] quotesLastDates = new DateTime[noAssets];

        //     for (int jAssets = 0; jAssets < quotesLastDates.Length; jAssets++)
        //     {
        //         quotesLastDates[jAssets] = quotesData1[jAssets][^1].Date;
        //     }

        //     double[] quotesFirstPrices = new double[noAssets];

        //     for (int jAssets = 0; jAssets < quotesFirstPrices.Length; jAssets++)
        //     {
        //         quotesFirstPrices[jAssets] = quotesData1[jAssets][0].AdjClosePrice;
        //     }

        //     double[] quotesLastPrices = new double[noAssets];

        //     for (int jAssets = 0; jAssets < quotesLastPrices.Length; jAssets++)
        //     {
        //         quotesLastPrices[jAssets] = quotesData1[jAssets][^1].AdjClosePrice;
        //     }

        //     IList<List<double>> quotesPrices = new List<List<double>>();

        //     for (int iAsset = 0; iAsset < noAssets; iAsset++)
        //     {
        //         int shiftDays = 0;
        //         List<double> assPriceSubList = new();
        //         //for (int jRows = 0; jRows < noBtDays; jRows++)
        //         //{
        //         int jRows = 0;
        //             while (quotesDateVec[jRows] < quotesFirstDates[iAsset])
        //             {
        //                 assPriceSubList.Add(quotesFirstPrices[iAsset]);
        //                 shiftDays += 1;
        //                 jRows++;
        //                 if (jRows >= noBtDays)
        //                 {
        //                     break;
        //                 }
        //             }
        //             while (quotesDateVec[jRows] == quotesData1[iAsset][jRows - shiftDays].Date)
        //             {
        //                 assPriceSubList.Add(quotesData1[iAsset][jRows - shiftDays].AdjClosePrice);
        //                 jRows++;
        //                 if (jRows >= quotesData1[iAsset].Count+shiftDays)
        //                 {
        //                     break;
        //                 }
        //             }
        //             if (jRows < noBtDays)
        //             {
        //                 while (quotesDateVec[jRows] > quotesLastDates[iAsset])
        //                 {
        //                     assPriceSubList.Add(quotesLastPrices[iAsset]);
        //                     jRows++;
        //                     if (jRows >= noBtDays)
        //                     {
        //                         break;
        //                     }
        //                 }
        //             }
        //         //}
        //         quotesPrices.Add(assPriceSubList);
        //     }

        //     double[,] histRet = new double[retLB.Length,noAssets];

        //     for (int iAsset = 0; iAsset < noAssets; iAsset++)
        //     {
        //         for (int jRows = 0; jRows < retLB.Length; jRows++)
        //         {
        //             histRet[jRows,iAsset]=quotesPrices[iAsset][quotesPrices[0].Count-1] / quotesPrices[iAsset][quotesPrices[0].Count - 1-retLB[jRows]] - 1;
        //         }
        //     }

        //     int histRetLengthSum = retLB.Sum();
        //     double[,] histRet2 = new double[histRetLengthSum, noAssets];

        //     int kShift = 0;
        //     for (int kLen = 0; kLen < retLB.Length; kLen++)
        //     {
        //         for (int iAsset = 0; iAsset < noAssets; iAsset++)
        //         {
        //             for (int jRows = 0; jRows < retLB[kLen]; jRows++)
        //             {
        //                 histRet2[kShift+jRows, iAsset] = quotesPrices[iAsset][quotesPrices[0].Count - retLB[kLen] + jRows] / quotesPrices[iAsset][quotesPrices[0].Count - 1 - retLB[kLen]] - 1;
        //             }
        //         }
        //         kShift += retLB[kLen];
        //     }

        //     IList<List<double>> quotesRets = new List<List<double>>(); 

        //     for (int iAsset = 0; iAsset < noAssets; iAsset++)
        //     {
        //         List<double> assSubList = new();
        //         assSubList.Add(0);
        //         for (int jRows = 1; jRows < noBtDays; jRows++)
        //         {
        //             assSubList.Add(quotesPrices[iAsset][jRows]/quotesPrices[iAsset][jRows-1]-1);
        //         }
        //         quotesRets.Add(assSubList);
        //     }

        //     IList<List<double>> assVolDrags = new List<List<double>>();
        //     List<double> vixQuotes = new();
        //     double[] vixLevel = new double[noBtDays];
        //     if (quotesData2.Count < noBtDays)
        //     {
        //         quotesData2.Add(quotesData2[^1]);
        //     }

        //     for (int iRows = 0; iRows < noBtDays; iRows++)
        //     {
        //         vixQuotes.Add(quotesData2[iRows].AdjClosePrice);
        //     }

        //     for (int iAsset = 0; iAsset < noAssets; iAsset++)
        //     {
        //         List<double> assVolDragSubList = new();
        //         for (int jRows = 0; jRows < volLBPeriod-1; jRows++)
        //         {
        //             assVolDragSubList.Add(0);
        //             vixLevel[jRows] = Math.Round(Utils.Mean(vixQuotes.GetRange(0,jRows).ToArray()),3);
        //         }
        //         for (int jRows = volLBPeriod-1; jRows < noBtDays; jRows++)
        //         {
        //             assVolDragSubList.Add(Utils.Variance(quotesRets[iAsset].GetRange(jRows - volLBPeriod + 1, volLBPeriod).ToArray())/2*21);
        //             vixLevel[jRows] = Math.Round(Utils.Mean(vixQuotes.GetRange(jRows - volLBPeriod + 1, volLBPeriod).ToArray()),3);
        //         }
        //         assVolDrags.Add(assVolDragSubList);
        //     }
        //     vixLevel[0] = quotesData2[0].AdjClosePrice;

        //     string[] dateYearsVec = new string[noBtDays];
        //     string[] dateYearsMonthsVec = new string[noBtDays];
        //     for (int iRows = 0; iRows < dateYearsMonthsVec.Length; iRows++)
        //     {
        //         dateYearsVec[iRows] = quotesDateVec[iRows].ToString("yyyy");
        //         dateYearsMonthsVec[iRows] = quotesDateVec[iRows].ToString("yyyy-MM");
        //     }

        //     //Tuple<string[], string[], IList<List<double>>> dataToCumm = Tuple.Create(dateYearsVec, dateYearsMonthsVec, assVolDrags);

        //     string[] dateYearsDist = dateYearsVec.Distinct().ToArray();
        //     string[] dateYearsMonthsDist = dateYearsMonthsVec.Distinct().ToArray();

        //     double[,] dateYearsAvgs = new double[dateYearsDist.Length, noAssets];
        //     double[] dateYearsVixAvgs = new double[dateYearsDist.Length];
        //     int[] dateYearsCount = new int[dateYearsDist.Length];
        //     int kElem = 0;
        //     for (int iRows = 0; iRows < dateYearsDist.Length; iRows++)
        //     {
        //         double[] subSumVec = new double[noAssets];
        //         double subSumVix = 0;
        //         while (kElem<noBtDays && dateYearsVec[kElem]==dateYearsDist[iRows])
        //         {
        //             for (int jAssets = 0; jAssets < noAssets; jAssets++)
        //             {
        //                 subSumVec[jAssets] = subSumVec[jAssets]+assVolDrags[jAssets][kElem];
        //             }
        //             subSumVix += vixLevel[kElem];
        //             kElem++;
        //             dateYearsCount[iRows] += 1;
        //         }
        //         for (int jAssets = 0; jAssets < noAssets; jAssets++)
        //         {
        //             dateYearsAvgs[iRows, jAssets] = subSumVec[jAssets]/dateYearsCount[iRows];
        //         }
        //         dateYearsVixAvgs[iRows] = subSumVix / dateYearsCount[iRows];
        //     }
        //     int noTotalDays = dateYearsCount.Sum();

        //     double[,] dateYearsMonthsAvgs = new double[dateYearsMonthsDist.Length, noAssets];
        //     double[] dateYearsMonthsVixAvgs = new double[dateYearsMonthsDist.Length];
        //     int[] dateYearsMonthsCount = new int[dateYearsMonthsDist.Length];
        //     int kElemM = 0;
        //     for (int iRows = 0; iRows < dateYearsMonthsDist.Length; iRows++)
        //     {
        //         double[] subSumVec = new double[noAssets];
        //         double subSumVix = 0;
        //         while (kElemM < noBtDays && dateYearsMonthsVec[kElemM] == dateYearsMonthsDist[iRows])
        //         {
        //             for (int jAssets = 0; jAssets < noAssets; jAssets++)
        //             {
        //                 subSumVec[jAssets] = subSumVec[jAssets] + assVolDrags[jAssets][kElemM];
        //             }
        //             subSumVix += vixLevel[kElemM];
        //             kElemM++;
        //             dateYearsMonthsCount[iRows] += 1;
        //         }
        //         for (int jAssets = 0; jAssets < noAssets; jAssets++)
        //         {
        //             dateYearsMonthsAvgs[iRows, jAssets] = subSumVec[jAssets] / dateYearsMonthsCount[iRows];
        //         }
        //         dateYearsMonthsVixAvgs[iRows] = subSumVix / dateYearsMonthsCount[iRows];
        //     }

        //     double vixAvgTotal = Utils.Mean(vixLevel);
        //     double[] volDragsAvgsTotal = new double[noAssets];
        //     for (int jAssets = 0; jAssets < noAssets; jAssets++)
        //     {
        //         int numEl = 0;
        //         double subSum = 0;
        //         for (int iRows = 0; iRows < noBtDays; iRows++)
        //             if (assVolDrags[jAssets][iRows] > 0)
        //             {
        //                 subSum += assVolDrags[jAssets][iRows];
        //                 numEl += 1;
        //             }

        //         volDragsAvgsTotal[jAssets] = subSum/numEl;
        //     }

        //     //Request time (UTC)
        //     DateTime liveDateTime = DateTime.UtcNow;
        //     string liveDate = liveDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        //     DateTime timeNowET = Utils.ConvertTimeFromUtcToEt(liveDateTime);
        //     string liveDateString = "Request time (UTC): " + liveDate;

        //     //Last data time (UTC)
        //     string lastDataTime = (quotesData[0][^1].Date.Date == liveDateTime.Date & timeNowET.TimeOfDay<=new DateTime(2000,1,1,16,15,0).TimeOfDay) ? "Live data at " + liveDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "Close price on "+ quotesData[0][^1].Date.ToString("yyyy-MM-dd");
        //     string lastDataTimeString = "Last data time (UTC): "+lastDataTime;



        //     ////Creating input string for JavaScript.
        //     StringBuilder sb = new ("{" + Environment.NewLine);
        //     sb.Append(@"""requestTime"": """ + liveDateString);
        //     sb.Append(@"""," + Environment.NewLine + @"""lastDataTime"": """ + lastDataTimeString);

        //     sb.Append(@"""," + Environment.NewLine + @"""volLBPeri"": """ + volLBPeriod);
        //     sb.Append(@"""," + Environment.NewLine + @"""retHistLBPeri"": """ + retHistLB);
            
        //     sb.Append(@"""," + Environment.NewLine + @"""retLBPeris"": """);
        //     for (int i = 0; i < retLB.Length - 1; i++)
        //         sb.Append(retLBStr[i] + ", ");
        //     sb.Append(retLBStr[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""retLBPerisNo"": """);
        //     for (int i = 0; i < retLB.Length - 1; i++)
        //         sb.Append(retLB[i] + ", ");
        //     sb.Append(retLB[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""assetNames"": """);
        //     for (int i = 0; i < usedAssetList.Length - 1; i++)
        //         sb.Append(usedAssetList[i] + ", ");
        //     sb.Append(usedAssetList[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""defCheckedList"": """);
        //     for (int i = 0; i < defaultCheckedList.Length - 1; i++)
        //         sb.Append(defaultCheckedList[i] + ", ");
        //     sb.Append(defaultCheckedList[^1]);

            

        //     sb.Append(@"""," + Environment.NewLine + @"""volAssetNames"": """);
        //     for (int i = 0; i < volAssetListNN.Length - 1; i++)
        //         sb.Append(volAssetListNN[i] + ", ");
        //     sb.Append(volAssetListNN[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""etpAssetNames"": """);
        //     for (int i = 0; i < etpAssetListNN.Length - 1; i++)
        //         sb.Append(etpAssetListNN[i] + ", ");
        //     sb.Append(etpAssetListNN[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""gchAssetNames"": """);
        //     for (int i = 0; i < gchAssetListNN.Length - 1; i++)
        //         sb.Append(gchAssetListNN[i] + ", ");
        //     sb.Append(gchAssetListNN[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""gmAssetNames"": """);
        //     for (int i = 0; i < gmAssetListNN.Length - 1; i++)
        //         sb.Append(gmAssetListNN[i] + ", ");
        //     sb.Append(gmAssetListNN[^1]);
            
        //     sb.Append(@"""," + Environment.NewLine + @"""quotesDateVector"": """);
        //     for (int i = 0; i < quotesDateVec.Length - 1; i++)
        //         sb.Append(quotesDateVec[i].ToString("yyyy-MM-dd") + ", ");
        //     sb.Append(quotesDateVec[^1].ToString("yyyy-MM-dd"));

        //     sb.Append(@"""," + Environment.NewLine + @"""dailyVolDrags"": """);
        //     for (int i = 0; i < assVolDrags[0].Count; i++)
        //     {
        //         sb.Append("");
        //         for (int j = 0; j < assVolDrags.Count - 1; j++)
        //         {
        //             sb.Append(Math.Round(assVolDrags[j][i]*100,2).ToString() + "%, ");
        //         }
        //         sb.Append(Math.Round(assVolDrags[assVolDrags.Count - 1][i]*100,2).ToString() + "%");
        //         if (i < assVolDrags[0].Count - 1)
        //         {
        //             sb.Append("ß ");
        //         }
        //     }

        //     sb.Append(@"""," + Environment.NewLine + @"""dailyVIXMas"": """);
        //     for (int i = 0; i < vixLevel.Length - 1; i++)
        //         sb.Append(Math.Round(vixLevel[i],2).ToString() + ", ");
        //     sb.Append(Math.Round(vixLevel[^1],2));

        //     sb.Append(@"""," + Environment.NewLine + @"""yearList"": """);
        //     for (int i = 0; i < dateYearsDist.Length - 1; i++)
        //         sb.Append(dateYearsDist[i] + ", ");
        //     sb.Append(dateYearsDist[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""yearMonthList"": """);
        //     for (int i = 0; i < dateYearsMonthsDist.Length - 1; i++)
        //         sb.Append(dateYearsMonthsDist[i] + ", ");
        //     sb.Append(dateYearsMonthsDist[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""yearlyAvgs"": """);
        //     for (int i = 0; i < dateYearsAvgs.GetLength(0); i++)
        //     {
        //         sb.Append("");
        //         for (int j = 0; j < dateYearsAvgs.GetLength(1) - 1; j++)
        //         {
        //             sb.Append(Math.Round(dateYearsAvgs[i,j] * 100, 2).ToString() + "%, ");
        //         }
        //         sb.Append(Math.Round(dateYearsAvgs[i,dateYearsAvgs.GetLength(1)-1] * 100, 2).ToString() + "%");
        //         if (i < dateYearsAvgs.GetLength(0)-1)
        //         {
        //             sb.Append("ß ");
        //         }
        //     }

        //     sb.Append(@"""," + Environment.NewLine + @"""monthlyAvgs"": """);
        //     for (int i = 0; i < dateYearsMonthsAvgs.GetLength(0); i++)
        //     {
        //         sb.Append("");
        //         for (int j = 0; j < dateYearsMonthsAvgs.GetLength(1) - 1; j++)
        //         {
        //             sb.Append(Math.Round(dateYearsMonthsAvgs[i, j] * 100, 2).ToString() + "%, ");
        //         }
        //         sb.Append(Math.Round(dateYearsMonthsAvgs[i, dateYearsMonthsAvgs.GetLength(1) - 1] * 100, 2).ToString() + "%");
        //         if (i < dateYearsMonthsAvgs.GetLength(0) - 1)
        //         {
        //             sb.Append("ß ");
        //         }
        //     }

        //     sb.Append(@"""," + Environment.NewLine + @"""yearlyVIXAvgs"": """);
        //     for (int i = 0; i < dateYearsVixAvgs.Length - 1; i++)
        //         sb.Append(Math.Round(dateYearsVixAvgs[i], 2).ToString() + ", ");
        //     sb.Append(Math.Round(dateYearsVixAvgs[^1], 2));

        //     sb.Append(@"""," + Environment.NewLine + @"""monthlyVIXAvgs"": """);
        //     for (int i = 0; i < dateYearsMonthsVixAvgs.Length - 1; i++)
        //         sb.Append(Math.Round(dateYearsMonthsVixAvgs[i], 2).ToString() + ", ");
        //     sb.Append(Math.Round(dateYearsMonthsVixAvgs[^1], 2));

        //     sb.Append(@"""," + Environment.NewLine + @"""yearlyCounts"": """);
        //     for (int i = 0; i < dateYearsCount.Length - 1; i++)
        //         sb.Append(dateYearsCount[i].ToString() + ", ");
        //     sb.Append(dateYearsCount[^1]);
                        
        //     sb.Append(@"""," + Environment.NewLine + @"""noTotalDays"": """ + noTotalDays);

        //     sb.Append(@"""," + Environment.NewLine + @"""monthlyCounts"": """);
        //     for (int i = 0; i < dateYearsMonthsCount.Length - 1; i++)
        //         sb.Append(dateYearsMonthsCount[i].ToString() + ", ");
        //     sb.Append(dateYearsMonthsCount[^1]);

        //     sb.Append(@"""," + Environment.NewLine + @"""vixAvgTotal"": """ + Math.Round(vixAvgTotal,2).ToString());

        //     sb.Append(@"""," + Environment.NewLine + @"""volDragsAvgsTotalVec"": """);
        //     for (int i = 0; i < volDragsAvgsTotal.Length - 1; i++)
        //         sb.Append(Math.Round(volDragsAvgsTotal[i]*100,2).ToString() + "%, ");
        //     sb.Append(Math.Round(volDragsAvgsTotal[^1]*100,2).ToString() + "%");

        //     sb.Append(@"""," + Environment.NewLine + @"""histRetMtx"": """);
        //     for (int i = 0; i < histRet.GetLength(0); i++)
        //     {
        //         sb.Append("");
        //         for (int j = 0; j < histRet.GetLength(1) - 1; j++)
        //         {
        //             sb.Append(Math.Round(histRet[i, j] * 100, 2).ToString() + "%, ");
        //         }
        //         sb.Append(Math.Round(histRet[i, histRet.GetLength(1) - 1] * 100, 2).ToString() + "%");
        //         if (i < histRet.GetLength(0) - 1)
        //         {
        //             sb.Append("ß ");
        //         }
        //     }

        //     sb.Append(@"""," + Environment.NewLine + @"""histRet2Chart"": """);
        //     for (int i = 0; i < histRet2.GetLength(0); i++)
        //     {
        //         sb.Append("");
        //         for (int j = 0; j < histRet2.GetLength(1) - 1; j++)
        //         {
        //             sb.Append(Math.Round(histRet2[i, j] * 100, 2).ToString() + "%, ");
        //         }
        //         sb.Append(Math.Round(histRet2[i, histRet2.GetLength(1) - 1] * 100, 2).ToString() + "%");
        //         if (i < histRet2.GetLength(0) - 1)
        //         {
        //             sb.Append("ß ");
        //         }
        //     }

        //     sb.AppendLine(@"""" + Environment.NewLine + @"}");

        //     // var asdfa = sb.ToString(); //testing created string to JS

        //     return sb.ToString();
           
        // }
    }
}