using SqCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HealthMonitor
{
    public partial class HealthMonitor
    {
        struct DownloadFailStruct
        {
            public string url;
            public int maxAllowedFail;      // usually 0 fails are allowed
            public int nFail;

            public DownloadFailStruct(string p_url, int p_maxAllowedFail, int p_nFail)
            {
                url = p_url;
                maxAllowedFail = p_maxAllowedFail;
                nFail = p_nFail;
            }
        };

        DownloadFailStruct[] cWebsitesToCheckAndnFail = {
            new DownloadFailStruct("https://sqcore.net/WebServer/ping", 0, 0),
            new DownloadFailStruct("http://www.snifferquant.com/dac/", 0, 0),
            new DownloadFailStruct("https://www.snifferquant.net/WebServer/ping", 1, 0)         // 1 fail is fine. If it fails second time, send Email. The reason is that while we develop SQWebpage, and start/stop/deploy website, this error triggers too many times. (in DEVELOPMENT, it is better to give some leeway)
            };

        bool m_isCheckWebsitesServiceOutageEmailWasSent = false;  // to avoid sending the same warning email every 9 minutes; send only once

        public void CheckWebsitesAndKeepAliveTimer_Elapsed(object? p_sender) // Timer is coming on a ThreadPool thread
        {
            try
            {
                Utils.Logger.Info($"CheckWebsitesAndKeepAliveTimer_Elapsed(at every {cCheckWebsitesTimerFrequencyMinutes} minutes) BEGIN");

                List<string> failedWebsites = new List<string>();
                for (int i = 0; i < cWebsitesToCheckAndnFail.Length; i++)
                {
                    string hmWebsiteStr = String.Empty;
                    if (Utils.DownloadStringWithRetry(cWebsitesToCheckAndnFail[i].url, out hmWebsiteStr, 5, TimeSpan.FromSeconds(5), false))
                    {
                        cWebsitesToCheckAndnFail[i].nFail = 0;
                        Utils.Logger.Info(cWebsitesToCheckAndnFail[i].url + " returned: " + (hmWebsiteStr.Substring(0, (hmWebsiteStr.Length > 45) ? 45 : hmWebsiteStr.Length)).Replace("\r\n", "").Replace("\n", "")); // it is better to see it as one line in the log file
                    }
                    else
                    {
                        Utils.Logger.Info("Failed download multiple (5x) times :" + cWebsitesToCheckAndnFail[i].url);

                        if (cWebsitesToCheckAndnFail[i].nFail >= cWebsitesToCheckAndnFail[i].maxAllowedFail)
                        {
                            failedWebsites.Add(cWebsitesToCheckAndnFail[i].url);
                        }
                        else
                        {
                            cWebsitesToCheckAndnFail[i].nFail++;
                        }
                    }
                }

                bool isOK = (failedWebsites.Count == 0);
                if (!isOK)
                {
                    Utils.Logger.Info("CheckWebsitesAndKeepAliveTimer(): !isOK.");
                    if (!m_isCheckWebsitesServiceOutageEmailWasSent)
                    {
                        Utils.Logger.Info("CheckWebsitesAndKeepAliveTimer(). Sending Warning email.");
                        new Email
                        {
                            ToAddresses = Utils.Configuration["Emails:Gyant"],
                            Subject = "SQ HealthMonitor: WARNING! CheckWebsites was NOT successfull.",
                            Body = "SQ HealthMonitor: WARNING! The following downloads are failed multiple (5x) times: " + String.Join(",", failedWebsites.ToArray()),
                            IsBodyHtml = false
                        }.Send();
                        m_isCheckWebsitesServiceOutageEmailWasSent = true;
                    }
                }
                else
                {
                    Utils.Logger.Info("CheckWebsitesAndKeepAliveTimer(): isOK.");
                    if (m_isCheckWebsitesServiceOutageEmailWasSent)
                    {  // it was bad, but now it is correct somehow
                        new Email
                        {
                            ToAddresses = Utils.Configuration["Emails:Gyant"],
                            Subject = "SQ HealthMonitor: OK! CheckWebsites was successfull again.",
                            Body = "SQ HealthMonitor: OK! CheckWebsites was successfull again.",
                            IsBodyHtml = false
                        }.Send();
                        m_isCheckWebsitesServiceOutageEmailWasSent = false;
                    }
                }
            }
            catch (Exception e)
            {
                Utils.Logger.Error("Exception caught in CheckWebsites Timer. " + e.Message + " ,InnerException: " + ((e.InnerException != null) ? e.InnerException.Message : ""));
            }

            Utils.Logger.Info("CheckWebsitesAndKeepAliveTimer_Elapsed() END");
        }

    }
}
