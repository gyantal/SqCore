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

        [HttpGet]
        // public IEnumerable<ExampleMessage> Get()
        // {
        //     Thread.Sleep(5000);     // intentional delay to simulate a longer process to crunch data. This can be removed.

        //     var userEmailClaim = HttpContext?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        //     string email = userEmailClaim?.Value  ?? "Unknown email";

        //     var firstMsgToSend = new ExampleMessage
        //     {
        //         MsgType = "AdminMsg",
        //         StringData = $"Cookies says your email is '{email}'.",
        //         DateOrTime = DateTime.Now,
        //         IntData = 0,                
        //     };

        //     string[] RandomStringDataToSend = new[]  { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
        //     var rng = new Random();
        //     return (new ExampleMessage[] { firstMsgToSend }.Concat(Enumerable.Range(1, 5).Select(index => new ExampleMessage
        //     {
        //         MsgType = "Msg-type",
        //         StringData = RandomStringDataToSend[rng.Next(RandomStringDataToSend.Length)],
        //         DateOrTime = DateTime.Now.AddDays(index),
        //         IntData = rng.Next(-20, 55)                
        //     }))).ToArray();
        // }

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
                ""gmAssetNames"": ""MDY, ILF, FEZ, EEM, EPP, VNQ""
                }";
            return mockupTestResponse;
        }
    }
}