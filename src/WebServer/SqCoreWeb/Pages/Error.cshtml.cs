using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace SqCoreWeb.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorModel : PageModel
    {
#pragma warning disable IDE0052 // keep example in the code for future reference (IDE0052: 'Private member can be removed as the value assigned to it is never read')
        private readonly ILogger<ErrorModel> m_loggerKestrelStyleDontUse; // Kestrel sends the logs to AspLogger, which will send it back to NLog. It can be used, but practially never use it. Even though this is the official ASP practice. It saves execution resource to not use it. Also, it is more consistent to use Utils.Logger global everywhere in our code.
#pragma warning restore IDE0052
        public ErrorModel(ILogger<ErrorModel> logger)
        {
            m_loggerKestrelStyleDontUse = logger;
        }

        public string RequestId { get; set; } = string.Empty;

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }
    }
}
