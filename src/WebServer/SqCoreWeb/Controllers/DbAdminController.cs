using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SqCommon;
using FinTechCommon;

namespace SqCoreWeb.Controllers
{
    public class DbAdminController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly ILogger<Program> m_loggerKestrelStyleDontUse; // Kestrel sends the logs to AspLogger, which will send it back to NLog. It can be used, but practially never use it. Even though this is the official ASP practice. It saves execution resource to not use it. Also, it is more consistent to use Utils.Logger global everywhere in our code.
        private readonly IConfigurationRoot m_configKestrelStyleDontUse; // use the global Utils.Configuration instead. That way you don't have to pass down further in the call stack later
        private readonly IWebAppGlobals m_webAppGlobals;

        public DbAdminController(ILogger<Program> p_logger, IConfigurationRoot p_config, IWebAppGlobals p_webAppGlobals)
        {
            m_loggerKestrelStyleDontUse = p_logger;
            m_configKestrelStyleDontUse = p_config;
            m_webAppGlobals = p_webAppGlobals;
        }

        [HttpGet]
        public ActionResult RedisPing()
        {
            string result = MemDb.gMemDb.TestRedisExecutePing();
            return Content($"<HTML><body>Success! <br>MemDb.gMemDb.TestRedisExecutePing() has executed without exception.<br>Returned: '{result}'.</body></HTML>", "text/html");
        }

        [HttpGet]
        public ActionResult MemDbActiveRedisDbInd()
        {
            int result = MemDb.gMemDb.RedisDbIdx;
            return Content($"<HTML><body>Success! <br>MemDb.gMemDb.RedisDbInd has executed without exception.<br>Returned: '{result}', which means DB-{result} is used.</body></HTML>", "text/html");
        }

    }
}