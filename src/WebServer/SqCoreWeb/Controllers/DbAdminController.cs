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
    public class DbAdminController : Controller
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

        [HttpGet]   // Ping is accessed by the HealthMonitor every 9 minutes (to keep it alive), no no GoogleAuth there
        public ActionResult MirrorProdDb()
        {
            var targetDb = HttpContext.Request.Query["targetDb"];
            MemDb.gMemDb.MirrorProdDb(targetDb);

            return Content($"<HTML><body>Success! <br> MemDb.gMemDb.MirrorProdDb({targetDb}) has executed without exception. </body></HTML>", "text/html");
        }

        
        // public ActionResult OverwriteProdDb()
        // Don't implement mirror from DEV-1 to PROD. Too dangerous.
        // Don't write code for it. Do manually, so we are more careful. RedisDesktop + Redis CLI commands.
        // RedisDesktop is error prone when copying binary. Or when coping big text to Clipboard. Try to not use it.
        // Also, don't copy separate hash-keys one by one. . Instead copy the whole Hash in one go.
        // From Redis CLI:  (Backup DB0 to DB2 before overwriting it with DB1)
        // SELECT 0  // choose DB-0  (PROD database)
        // COPY memDb memDb DB 2 REPLACE   // copy hash to target DB-2 as a backup
        // SELECT 1  // choose DB-1  (DEV-1 database)
        // COPY memDb memDb DB 0 REPLACE   // copy hash to target DB-0 (the PROD database)

        [HttpGet]   // Ping is accessed by the HealthMonitor every 9 minutes (to keep it alive), no no GoogleAuth there
        public ActionResult UpsertAssets()
        {
            var targetDb = HttpContext.Request.Query["targetDb"];
            MemDb.gMemDb.UpsertAssets(targetDb);

            return Content($"<HTML><body>Success! <br> MemDb.gMemDb.UpsertAssets({targetDb}) has executed without exception. </body></HTML>", "text/html");
        }
    }
}