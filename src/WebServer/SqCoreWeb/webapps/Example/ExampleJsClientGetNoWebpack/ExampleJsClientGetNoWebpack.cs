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
    [ResponseCache(CacheProfileName = "DefaultMidDuration")]
    public class ExampleJsClientGetNoWebpackController : ControllerBase
    {
        public class ExampleMessage
        {
            public string MsgType { get; set; } = string.Empty;

            public string StringData { get; set; } = string.Empty;
            public DateTime DateOrTime { get; set; }

            public int IntData { get; set; }

            public int IntDataFunction => 32 + (int)(IntData / 0.5556);
        }

        public ExampleJsClientGetNoWebpackController()
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
    }
}