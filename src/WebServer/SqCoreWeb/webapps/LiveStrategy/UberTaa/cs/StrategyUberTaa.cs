using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SqCoreWeb.Controllers
{    
    [ApiController]
    [Route("[controller]")]
    [ResponseCache(CacheProfileName = "NoCache")]
    public class StrategyUberTaaController : ControllerBase
    {
        public class ExampleMessage
        {
            public string MsgType { get; set; } = string.Empty;

            public string StringData { get; set; } = string.Empty;
            public DateTime DateOrTime { get; set; }

            public int IntData { get; set; }

            public int IntDataFunction => 32 + (int)(IntData / 0.5556);
        }

        public StrategyUberTaaController()
        {
        }

        [HttpGet]
        public IEnumerable<ExampleMessage> Get()
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
        public ActionResult Index(int commo)
        {
            try
            {
                switch (commo)
                {
                    case 1: //GameChanger
                        return Content(GetStr(1), "text/html");
                    case 2: //Global Asset
                        return Content(GetStr(2), "text/html");
                    default:
                        break;
                }
                return Content(GetStr2(), "text/html");
            }
            catch
            {
                return Content(GetStr2(), "text/html");
            }
        }

        public string GetStr2()
        {
            return "Error";
        }
        public string GetStr(int p_basketSelector)
        {
            // throw new NotImplementedException();
             //Defining asset lists.
            // string[] clmtAssetList = new string[]{ "^GSPC", "XLU", "VTI" };
            // string[] gchAssetList = new string[]{ "AAPL", "ADBE", "AMZN", "BABA", "CRM", "CRWD", "ETSY", "FB", "GOOGL", "ISRG", "MA", "MELI", "MSFT", "NFLX", "NOW", "NVDA", "PYPL", "QCOM", "ROKU", "SE", "SHOP", "SQ", "TDOC", "TWLO", "V", "ZM", "TLT"}; //TLT is used as a cashEquivalent
            // string[] gmrAssetList = new string[] { "MDY", "ILF", "FEZ", "EEM", "EPP", "VNQ", "TLT" }; //TLT is used as a cashEquivalent
            // string[] usedAssetList = Array.Empty<string>();
            // string titleString;
            // switch (p_basketSelector)
            // {
            //     case 1:
            //         usedAssetList = gchAssetList;
            //         titleString = "GameChangers";
            //         break;
            //     case 2:
            //         usedAssetList = gmrAssetList;
            //         titleString = "Global Assets";
            //         break;
            // }
            // string[] allAssetList = new string[clmtAssetList.Length+usedAssetList.Length];
            // clmtAssetList.CopyTo(allAssetList, 0);
            // usedAssetList.CopyTo(allAssetList, clmtAssetList.Length);

            // string gchGSheetRef = "https://sheets.googleapis.com/v4/spreadsheets/1AGci_xFhgcC-Q1tEZ5E-HTBWbOU-C9ZXyjLIN1bEZeE/values/A1:AF2000?key=";
            // string gmrGSheetRef = "https://sheets.googleapis.com/v4/spreadsheets/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU/values/A1:Z2000?key=";
            // string gchGSheet2Ref = "https://docs.google.com/spreadsheets/d/1AGci_xFhgcC-Q1tEZ5E-HTBWbOU-C9ZXyjLIN1bEZeE/edit?usp=sharing";
            // string gmrGSheet2Ref = "https://docs.google.com/spreadsheets/d/1ugql_-IXXVrU7M2TtU4wPaDELH5M6NQXy82fwZgY2yU/edit?usp=sharing";
            // string gchGDocRef = "https://docs.google.com/document/d/1JPyRJY7VrW7hQMagYLtB_ruTzEKEd8POHQy6sZ_Nnyk/edit?usp=sharing";
            // string gmrGDocRef = "https://docs.google.com/document/d/1-hDoFu1buI1XHvJZyt6Cq813Hw1TQWGl0jE7mwwS3l0/edit?usp=sharing";

            // string usedGSheetRef = (p_basketSelector==1) ? gchGSheetRef : gmrGSheetRef;
            // string usedGSheet2Ref = (p_basketSelector == 1) ? gchGSheet2Ref : gmrGSheet2Ref;
            // string usedGDocRef = (p_basketSelector == 1) ? gchGDocRef : gmrGDocRef;
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
    }
}