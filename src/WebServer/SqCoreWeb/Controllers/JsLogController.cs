using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SqCommon;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

namespace SqCoreWeb.Controllers
{
    public enum NgxLoggerLevel
    {
        TRACE = 0,
        DEBUG,
        INFO,
        LOG,
        WARN,
        ERROR,
        FATAL,
        OFF
    }

    public class NGXLogInterface
    {
        public NgxLoggerLevel level { get; set; }
        public string timestamp { get; set; } = string.Empty;
        public string fileName { get; set; }  = string.Empty;
        public string lineNumber { get; set; }  = string.Empty;
        public string message { get; set; }  = string.Empty;
        public object[] additional { get; set; } = new object[] { };
    }

    // Logger for Javascript code. This can notify Healthmonitor if Crash occurs in HTML JS in the client side.
    public class JsLogController : Controller
    {
        // http://stackoverflow.com/questions/16996713/receiving-a-http-post-in-http-handler
        [HttpGet, HttpPost]
        public async Task<ActionResult> Index()
        {
            string jsLogMessage = string.Empty;
            using (var reader = new StreamReader(Request.Body))
            {
                // example: '{"message":"A simple error() test message to NGXLogger","additional":[],"level":5,"timestamp":"2020-01-18T00:46:47.740Z","fileName":"ExampleJsClientGet.js","lineNumber":"52"}'
                jsLogMessage = await reader.ReadToEndAsync();
            }

            // 1. just log the event to our file log
            var clientIP = WsUtils.GetRequestIPv6(this.HttpContext);
            var clientUserEmail = WsUtils.GetRequestUser(this.HttpContext);
            if (clientUserEmail == null)
                clientUserEmail = "UnknownUser@gmail.com";

            string jsLogMsgWithOrigin = $"Javascript Logger /JsLogController was called by '{clientUserEmail}' from '{clientIP}'. Received JS log: '{jsLogMessage}'";
            Utils.Logger.Info(jsLogMsgWithOrigin);

            // 2. interpret the log and if it is an error, notify HealthMonitor
            try
            {
                var jsLogObj = JsonSerializer.Deserialize<NGXLogInterface>(jsLogMessage);
                if (jsLogObj == null || jsLogObj.level == NgxLoggerLevel.ERROR || jsLogObj.level == NgxLoggerLevel.FATAL)
                {   // notify HealthMonitor to send an email
                    await HealthMonitorMessage.SendAsync(jsLogMsgWithOrigin, HealthMonitorMessageID.SqCoreWebJsError);
                }
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "JsLogController(). Probably serialization problem. JsLogMessage: " + jsLogMessage);
                throw;  // if we don't rethrow it, Kestrel will not send HealthMonitor message. Although we should fix this error.
            }

            return NoContent(); // The common use case is to return 204 (NoContent) as a result of a PUT request, updating a resource
        }
    }
}
