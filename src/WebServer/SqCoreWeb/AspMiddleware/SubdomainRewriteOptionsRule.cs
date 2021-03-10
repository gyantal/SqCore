using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using SqCommon;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static SqCoreWeb.WsUtils;

namespace SqCoreWeb
{

    public class SubdomainRewriteOptionsRule : IRule
    {
        // Request "dashboard.sqcore.net/index.html" should be converted to "sqcore.net/webapps/MarketDashboard/index.html"
        public void ApplyRule(RewriteContext context)
        {
            // Utils.Logger.Info("SubdomainRewriteOptionsRule(): Request with host: " + (req.IsHttps ? "https://" : "http://") + currentHost + req.PathBase + req.Path + req.QueryString);
            ApplyRuleForSubdomain(context, "dashboard.", "/webapps/MarketDashboard");
            ApplyRuleForSubdomain(context, "healthmonitor.", "/webapps/HealthMonitor");
            ApplyRuleForSubdomain(context, "tools.", "/webapps/Tools");
        }
        // idea from https://ryanwilliams.io/blog/redirecting-www-and-non-https-traffic-with-asp-net-core-2-0
        private static void ApplyRuleForSubdomain(RewriteContext context, string p_subdomain, string p_pathPrefixReplacement)
        {
            var req = context.HttpContext.Request;
            var currentHost = req.Host;
            bool isRedirect = false;  // it can be Redirect Page or silent Rewrite URL (user will not even notice it)

            if (currentHost.Host.StartsWith(p_subdomain))
            {
                // Before this SubdomainRewriteOptionsRule, all subdomain calls went to the same main index.html
                // MVC and other StaticFile routers didn't differentiated based on subdomain.
                // https://sqcore.net, https://dashboard.sqcore.net, https://healthmonitor.sqcore.net
                // After the redirection, keep the login, logout links, otherwise CheckAuthorizedGoogleEmail() email is '', and login is not possible.
                string path = req.Path.ToString();
                if (path.EndsWith("UserAccount/login", StringComparison.OrdinalIgnoreCase) || // https://healthmonitor.sqcore.net/UserAccount/login should work with its subdomain. Don't redirect that.
                    path.EndsWith("UserAccount/logout", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("signin-google", StringComparison.OrdinalIgnoreCase) || // Google calls back on https://healthmonitor.sqcore.net/signin-google
                    path.StartsWith("/ws/", StringComparison.OrdinalIgnoreCase) || // WebSocket listeners listen on "/ws/" from root
                    path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || // some controllers listen on /api
                    path.StartsWith("/WebServer/", StringComparison.OrdinalIgnoreCase)) // healthmonitor.sqcore.net needs https://healthmonitor.sqcore.net/WebServer/ReportHealthMonitorCurrentStateToDashboardInJSON
                    return;

                Utils.Logger.Info("SubdomainRewriteOptionsRule(): Request with host: " + (req.IsHttps ? "https://" : "http://") + currentHost + req.PathBase + req.Path + req.QueryString);

                if (isRedirect)
                {
                    string newHost = currentHost.Host.Substring(p_subdomain.Length) + ((currentHost.Port != null) ? ((int)(currentHost.Port)).ToString() : String.Empty);
                    var newUrl = new StringBuilder().Append(req.IsHttps ? "https://" : "http://").Append(newHost).Append(req.PathBase).Append(p_pathPrefixReplacement).Append(req.Path).Append(req.QueryString);

                    Utils.Logger.Info("SubdomainRewriteOptionsRule(): Doing Redirection. NewUrl: " + newUrl);
                    context.HttpContext.Response.Redirect(newUrl.ToString(), false);     // Redirect is temporary (HTTP 302); Other option is redirect is permanent (HTTP 301), which means browser will cache it forever.
                    context.Result = RuleResult.EndResponse;
                }
                else // silent Rewrite URL (user will not even notice it)
                {
                    req.Host = (currentHost.Port != null) ?
                        new HostString(currentHost.Host.Substring(p_subdomain.Length), (int)(currentHost.Port)) :
                        new HostString(currentHost.Host.Substring(p_subdomain.Length));
                    req.Path = p_pathPrefixReplacement + req.Path;

                    Utils.Logger.Info("SubdomainRewriteOptionsRule(): Doing Silent Rewrite. NewUrl: " + (req.IsHttps ? "https://" : "http://") + req.Host + req.PathBase + req.Path + req.QueryString);
                }
            }
        }
    }
}