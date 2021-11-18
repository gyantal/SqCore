using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqCoreWeb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserAccountController : Microsoft.AspNetCore.Mvc.Controller
    {
        [HttpGet("[action]")]       // from the Route template "template: "{controller=Home}/{action=Index}/{id?}");" only action is used.
        public async Task Login(string? returnUrl) // (string returnUrl = "/")
        {
            await HttpContext.ChallengeAsync("Google",
                new AuthenticationProperties()
                {
                    // IsPersistent = true,
                    // AllowRefresh = true,
                    // ExpiresUtc = ? Leave them as default.
                    // subdomain https://healthmonitor.sqcore.net/UserAccount/login should redirect back to https://healthmonitor.sqcore.net/
                    RedirectUri = returnUrl ?? "/"      //  better in a short form, so don't do "/index.html" if http://localhost/api/UserAccount/login is called directly, there is no returnURL, which is null. However if we pass Null to GoogleAuth, it will come back to this "/login" which will cause an infinite loop. 
                });
        }

        [Authorize]
        [HttpGet("[action]")]
        public async Task Logout()
        {
            // TODO: redirect user to a nicer page that shows: ""You have been logged out. Goodbye " + context.User.Identity.Name + "<br>""
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, new AuthenticationProperties
            {
                // RedirectUri = Url.Action("Index", "Home")
                // subdomain https://healthmonitor.sqcore.net/UserAccount/logout should NOT redirect back to https://healthmonitor.sqcore.net/  because that requires user login and will automatically login back auto on Edge Browser.
                RedirectUri = (Program.g_webAppGlobals.KestrelEnv?.EnvironmentName == "Development") ? "https://localhost:5001/" : "//sqcore.net"
            });
        }

        [HttpGet("[action]")]
#if !DEBUG
        [Authorize]     // we can live without it, because ControllerCommon.CheckAuthorizedGoogleEmail() will redirect to /login anyway, but it is quicker that this automatically redirects without clicking another URL link.
#endif
        public async Task<ActionResult> UserInfo()
        {
            var userAuthCheck = WsUtils.CheckAuthorizedGoogleEmail(this.HttpContext);
            if (userAuthCheck != UserAuthCheckResult.UserKnownAuthOK)
            {
                return Content(@"<HTML><body>Google Authorization Is Required. You are not logged in or your your Google account is not accepted.<br>" +
                   @"<A href=""/logout"">logout this Google user </a> and login with another one." +
                    "</body></HTML>", "text/html");
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("<html><body>");
            sb.Append("Hello " + (User.Identity?.Name ?? "anonymous") + "<br>");
            sb.Append("Request.Path '" + (Request.Path.ToString() ?? "Empty") + "'<br>");
            foreach (var claim in User.Claims)
            {
                sb.Append(claim.Type + ": " + claim.Value + "<br>");
            }

            sb.Append("Tokens:<br>");
            sb.Append("Access Token: " + await HttpContext.GetTokenAsync("access_token") + "<br>");
            sb.Append("Refresh Token: " + await HttpContext.GetTokenAsync("refresh_token") + "<br>");
            sb.Append("Token Type: " + await HttpContext.GetTokenAsync("token_type") + "<br>");
            sb.Append("expires_at: " + await HttpContext.GetTokenAsync("expires_at") + "<br>");
            sb.Append("<a href=\"/logout\">Logout</a><br>");
            sb.Append("</body></html>");

            return Content(sb.ToString(), "text/html");
        }

        [HttpGet("[action]")]
        public string NoAuthNeedWebserviceSample()
        {
            var userEmailClaim = HttpContext?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            string email = userEmailClaim?.Value ?? "Unknown email";

            return "No authorization for this. For Testing purposes. Your email is: " + email;
        }

        [Authorize]     // this attribute will direct user to GoogleLogin, but in its pure form, it would allow any google email users to log in, so we need more secure than this.
        [HttpGet("[action]")]
        public IActionResult Profile()
        {
            // this accepts any Google logins
            return View();
        }

        [Authorize]
        [HttpGet("[action]")]
        public string SelectedGoogleUsersAuthNeedWebserviceSample()
        {
            var userEmailClaim = HttpContext?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            string email = userEmailClaim?.Value ?? "Unknown email";

            if (!String.Equals(email, "foo@google.com"))
                return "You don't belong to our precious users. Your email is: " + email;

            return "You are one of our precious users. Cheers. Your email is: " + email;
        }


    }
}