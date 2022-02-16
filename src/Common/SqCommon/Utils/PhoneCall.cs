using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqCommon
{
    public enum Caller { Gyantal, Charmat0 }

    // Twilio docs/examples in Azure documentation:
    // http://www.windowsazure.com/en-us/documentation/articles/twilio-dotnet-how-to-use-for-voice-sms/
    // Official API docs:
    // https://www.twilio.com/docs/api/rest

    // works under Mono, because it is a simple REST API call
    public class PhoneCall
    {
        public static Dictionary<Caller, string> PhoneNumbers = new();
        public static string TwilioSid = string.Empty;
        public static string TwilioToken = string.Empty;

        public string ToNumber = string.Empty;     // it is not an enum, because callees can be anybody, not only a fixed number of phoneNumbers
        public string Message = "Default message";
        public Caller FromNumber = Caller.Gyantal;      // enum, because we know all the FromNumbers, because that has to be registered with Twilio. Only a fixed, limited numbers can be Callers.
        public string ResultJSON = string.Empty;
        public string ErrorStr = string.Empty;
        public int NRepeatAll = 1; // with this setting = 1, the phone call say the Message only once. However it is common that we want that the message is repeated, so we use = 2.

        /// <summary> Returns true when Twilio's server accepted our request, BEFORE the phone begins to ring!
        /// Returns false if the server rejected our request with error message, or .NET exception occurred.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<bool> MakeTheCallAsync()
        {
            string caller = PhoneNumbers[FromNumber];
            if (caller == null)
                throw new ArgumentException(FromNumber.ToString(), "FromNumber");
            if (String.IsNullOrEmpty(ToNumber))
                throw new ArgumentException(ToNumber ?? "null", "ToNumber");
            string xml = string.Empty;
            if (NRepeatAll > 1)
            {
                var say = new System.Xml.XmlDocument().CreateElement("Say");
                say.InnerText = Message;
                xml = "<Response>" + String.Join("<Pause length=\"2\"/>", Enumerable.Repeat(say.OuterXml, NRepeatAll)) + "</Response>";
            }

            var client = new System.Net.Http.HttpClient();  // System.Net.Http.dll
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(TwilioSid + ":" + TwilioToken)));
            try
            {
                var content = new Dictionary<string, string?>()
                {
                    { "From", caller },
                    { "To",   ToNumber },
                    { "Method", "GET" },
                    {
                        "Url",  xml != null ? "http://twimlets.com/echo?Twiml=" + Uri.EscapeDataString(xml)
                                            : "http://twimlets.com/message?Message%5B0%5D=" + Uri.EscapeDataString(Message)
                    } // O.K.
                    // { "Url",  "http://twimlets.com/message?" + Uri.EscapeDataString("Message[0]=" + p_message) } // <Response/>  -- successful but empty call
                    // { "Url",  "http://twimlets.com/message?Message%5B0%5D=Hello+this+is+a+test+call+from+Twilio." }  // O.K.
                    // { "Url",  "http://twimlets.com/message?Message[0]=Hello%2C+this+is+a+test+call+from+Twilio." }  // Error: 11100 Invalid URL format
                    // { "Url",  "http://twimlets.com/message?Message[0]=Hello,+this+is+a+test+call+from+Twilio." }  // Error: 11100 Invalid URL format
                    // { "Url",  "http://twimlets.com/message?Message[0]=" + Uri.EscapeDataString(p_message) } // Error: 11100 Invalid URL format
                    // { "Url",  "http://www.snifferquant.com/gyantal/twimlet.xml" }  // O.K.
                }.Select(r => new KeyValuePair<string?, string?>(r.Key, r.Value));

                System.Net.Http.HttpResponseMessage response = await client.PostAsync(
                    "https://api.twilio.com/2010-04-01/Accounts/" + TwilioSid + "/Calls.json",  // could be .csv as well, see https://www.twilio.com/docs/api/rest/tips
                    new System.Net.Http.FormUrlEncodedContent(content));
                string responseStr = await response.Content.ReadAsStringAsync();
                if (responseStr.Contains("\"sid\":")) // Then it looks like a proper JSON answer. Then probably it is executed well. Other JSON parts: (\"status\": \"queued\")
                {
                    ResultJSON = responseStr;
                }
                else
                {
                    ErrorStr = responseStr;
                }
            }
            catch (Exception e)
            {
                ErrorStr = ToStringWithoutStackTrace(e);
                // Program.gLogger.Info("Error: " + Error);
                Console.WriteLine("Error: " + ErrorStr);
            }
            return String.IsNullOrEmpty(ErrorStr);
        }

        static string ToStringWithoutStackTrace(Exception e)
        {
            string s = e?.ToString() ?? string.Empty;
            return s[..Math.Min(s.Length, s.IndexOf("\n   at ") & int.MaxValue)];
        }
    }
}