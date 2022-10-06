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

namespace SqCoreWeb.Controllers;

public class DbAdminController : Microsoft.AspNetCore.Mvc.Controller
{
    public DbAdminController()
    {
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