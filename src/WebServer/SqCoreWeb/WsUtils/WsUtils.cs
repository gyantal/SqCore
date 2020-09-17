using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SqCommon;

namespace SqCoreWeb
{
    public enum UserAuthCheckResult { UserKnownAuthOK, UserKnownAuthNotEnugh, UserUnknown };
    
    public static partial class WsUtils
    {
        public static string GetRequestUser(HttpContext p_httpContext)
        {
            var userEmailClaim = p_httpContext?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            return userEmailClaim?.Value ?? String.Empty;
        }

        // Some fallback logic can be added to handle the presence of a Load Balancer.  or CloudFront. Checked: CloudFront uses X-Forwarded-For : "82.44.159.196"
        // http://stackoverflow.com/questions/28664686/how-do-i-get-client-ip-address-in-asp-net-core
        // Use IPv6 as it is more future proof. IPv4 can be packed into IPv6.
        public static string GetRequestIPv6(HttpContext p_httpContext, bool p_tryUseXForwardHeader = true)
        {
            // SignalR "wss://" protocol: Connection.RemoteIpAddress is "127.0.0.1"
            // WebSocket "wss://" protocol: Connection.RemoteIpAddress is "::ffff:127.0.0.1"   // ::ffff: is a subnet prefix for IPv4 (32 bit) addresses that are placed inside an IPv6 (128 bit) space.
            // https://stackoverflow.com/questions/57572020/authenticationhandler-context-connection-remoteipaddress-returns-ffff192

            string? remoteIP = String.Empty;
            if (p_tryUseXForwardHeader)
            {
                remoteIP = GetHeaderValueAsNullableReference<string>(p_httpContext, "X-Forwarded-For");       // Old standard, but used by AWS CloudFront
                // todo support new "Forwarded" header (2014) https://en.wikipedia.org/wiki/X-Forwarded-For
                if (String.IsNullOrWhiteSpace(remoteIP))
                    remoteIP = GetHeaderValueAsNullableReference<string>(p_httpContext, "Forwarded");     // new standard  (2014 RFC 7239)
                //if (String.IsNullOrWhiteSpace(remoteIP))
                //     remoteIP = GetHeaderValueAs<string>(p_controller, "REMOTE_ADDR");     // there are 10 more, but we have to support only CloudFront for CPU saving. We don't need others. http://stackoverflow.com/questions/527638/getting-the-client-ip-address-remote-addr-http-x-forwarded-for-what-else-coul

            }

            // one way to get IP
            //var connection = p_httpContext.Features.Get<IHttpConnectionFeature>();
            //var clientIP = connection?.RemoteIpAddress?.ToString();

            // another way to get it
            if (String.IsNullOrWhiteSpace(remoteIP) && p_httpContext?.Connection?.RemoteIpAddress != null)
                remoteIP = p_httpContext?.Connection?.RemoteIpAddress?.MapToIPv6().ToString() ?? String.Empty;

            return String.IsNullOrWhiteSpace(remoteIP) ? "<Unknown IP>" : remoteIP;
        }

        public static T? GetHeaderValueAsNullableReference<T>(HttpContext p_httpContext, string p_headerName) where T : class // string is class, not struct 
        {
            if (p_httpContext?.Request?.Headers?.TryGetValue(p_headerName, out StringValues values) ?? false)
            {
                string rawValues = values.ToString();   // writes out as Csv when there are multiple.

                if (!String.IsNullOrEmpty(rawValues))
                    return (T)Convert.ChangeType(values.ToString(), typeof(T));
            }
            return default(T);
        }

        public static UserAuthCheckResult CheckAuthorizedGoogleEmail(HttpContext p_httpContext)
        {
// #if DEBUG  // try to force User login even in Development. To catch login errors, we have to debug it the same way as he Production one.
//               return UserAuthCheckResult.UserKnownAuthOK;
// #else
            var email = WsUtils.GetRequestUser(p_httpContext);
            if (String.IsNullOrEmpty(email))
                return UserAuthCheckResult.UserUnknown;

            if (IsAuthorizedGoogleUsers(email))
                return UserAuthCheckResult.UserKnownAuthOK;
            else
                return UserAuthCheckResult.UserKnownAuthNotEnugh;               
//#endif
        }

        static List<string>? g_authorizedGoogleUsers = null;

        public static bool IsAuthorizedGoogleUsers(string p_email)
        {
            if (g_authorizedGoogleUsers == null)
            {
                g_authorizedGoogleUsers = new List<string>() {
                    Utils.Configuration["Emails:Gyant"].ToLower(),
                    Utils.Configuration["Emails:Gyant2"].ToLower(),
                    Utils.Configuration["Emails:Laci"].ToLower(),
                    Utils.Configuration["Emails:Balazs"].ToLower(),
                    Utils.Configuration["Emails:Sumi"].ToLower(),
                    Utils.Configuration["Emails:Bunny"].ToLower(),
                    Utils.Configuration["Emails:Tundi"].ToLower(),
                    Utils.Configuration["Emails:Lukacs"].ToLower(),
                    Utils.Configuration["Emails:Charm0"].ToLower(),
                    Utils.Configuration["Emails:Charm1"].ToLower(),
                    Utils.Configuration["Emails:Charm2"].ToLower(),
                    Utils.Configuration["Emails:Charm3"].ToLower(),
                    Utils.Configuration["Emails:JCharm1"].ToLower(),
                    Utils.Configuration["Emails:Brook"].ToLower(),
                    Utils.Configuration["Emails:Dinah1"].ToLower()
                };
            }
            bool isUserOK = g_authorizedGoogleUsers.Contains(p_email.ToLower());
            return isUserOK;
        }

    }
}