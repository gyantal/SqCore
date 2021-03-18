using SqCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace HealthMonitor
{
    public class HMtoDashboardData
    {
        public string StartDate = string.Empty;        // DateTime serialization, deserialization between C# and JS is a real pain. And at the end, we send strings anyway. Easier to do this way.
        public bool DailyEmailReportEnabled;

        public bool RtpsTimerEnabled;
        public int RtpsTimerFrequencyMinutes;
        public List<string>? RtpsDownloads;

        public bool ProcessingVBrokerMessagesEnabled;
        public List<string>? VBrokerReports;
        public List<string>? VBrokerDetailedReports;

        public string CommandToBackEnd = string.Empty;
        public string ResponseToFrontEnd = string.Empty;   // it is "OK" or the Error message
    }

    public partial class HealthMonitor
    {
        internal void CurrentStateToHealthMonitorWebsite(TcpClient p_tcpClient, TcpMessage p_message)
        {
            HMtoDashboardData? hmDataOut = null;
            var hmDataIn = Utils.LoadFromJSON<HMtoDashboardData>(p_message.ParamStr);
            if (hmDataIn == null)
            {
                Utils.Logger.Error($"Cannot convert hmMessage: '{p_message.ParamStr}'.");
                return;
            }

            if (String.Equals(hmDataIn.CommandToBackEnd, "OnlyGetData", StringComparison.CurrentCultureIgnoreCase))
            {
                hmDataOut = new HMtoDashboardData() { ResponseToFrontEnd = "OK" };
                ReportCurrentStateToWebsiteInJSON(hmDataOut);
            }
            else if (String.Equals(hmDataIn.CommandToBackEnd, "ApplyTheDifferences", StringComparison.CurrentCultureIgnoreCase))
            {
                // 1. apply new settings 
                ApplyTheDifferencesComingFromWebsite(hmDataIn);
                // 2. read out the current state 
                hmDataOut = new HMtoDashboardData() { ResponseToFrontEnd = "OK" };
                ReportCurrentStateToWebsiteInJSON(hmDataOut);
            }
            else
            {
                hmDataOut = new HMtoDashboardData() { ResponseToFrontEnd = "Error: unrecognised CommandToBackEnd" };
            }

            if (p_message.ResponseFormat == TcpMessageResponseFormat.JSON)
            {
                var jsonStr = Utils.SaveToJSON<HMtoDashboardData>(hmDataOut);
                BinaryWriter bw = new BinaryWriter(p_tcpClient.GetStream());
                bw.Write(jsonStr);
                //bw.Write(@"{""AppOk"":""OK"",""StartDate"":""1998-11-16T00:00:00"",""StartDateLoc"":""1998-11-16T00:00:00.000Z"",""StartDateTimeSpanStr"":"""",""DailyEmailReportEnabled"":false,""RtpsOk"":""OK"",""RtpsTimerEnabled"":false,""RtpsTimerFrequencyMinutes"":-999,""RtpsDownloads"":[""aaaaaaaaaaaaaaaaaaaaaaaaaa"",""b""],""VBrokerOk"":""OK"",""ProcessingVBrokerMessagesEnabled"":false,""VBrokerReports"":[""a"",""b""],""VBrokerDetailedReports"":[""a"",""b""],""CommandToBackEnd"":""OnlyGetData"",""ResponseToFrontEnd"":""OK""}");
            }
        }

        private void ApplyTheDifferencesComingFromWebsite(HMtoDashboardData p_hmDataIn)
        {
            if (m_persistedState == null)
                return;

            if (p_hmDataIn.DailyEmailReportEnabled != m_persistedState.IsDailyEmailReportEnabled)
            {
                m_persistedState.IsDailyEmailReportEnabled = p_hmDataIn.DailyEmailReportEnabled;
                Console.WriteLine("From Website: IsDailyEmailReportEnabled changed to " + m_persistedState.IsDailyEmailReportEnabled);
                Utils.Logger.Info("From Website: IsDailyEmailReportEnabled changed to " + m_persistedState.IsDailyEmailReportEnabled);
            }
            if (p_hmDataIn.RtpsTimerEnabled != m_persistedState.IsRealtimePriceServiceTimerEnabled)
            {
                m_persistedState.IsRealtimePriceServiceTimerEnabled = p_hmDataIn.RtpsTimerEnabled;
                Console.WriteLine("From Website: IsRealtimePriceServiceTimerEnabled changed to " + m_persistedState.IsRealtimePriceServiceTimerEnabled);
                Utils.Logger.Info("From Website: IsRealtimePriceServiceTimerEnabled changed to " + m_persistedState.IsRealtimePriceServiceTimerEnabled);
            }
            if (p_hmDataIn.ProcessingVBrokerMessagesEnabled != m_persistedState.IsProcessingVBrokerMessagesEnabled)
            {
                m_persistedState.IsProcessingVBrokerMessagesEnabled = p_hmDataIn.ProcessingVBrokerMessagesEnabled;
                Console.WriteLine("From Website: IsProcessingVBrokerMessagesEnabled changed to " + m_persistedState.IsProcessingVBrokerMessagesEnabled);
                Utils.Logger.Info("From Website: IsProcessingVBrokerMessagesEnabled changed to " + m_persistedState.IsProcessingVBrokerMessagesEnabled);
            }
        }

        private void ReportCurrentStateToWebsiteInJSON(HMtoDashboardData p_hmData)
        {
            if (m_persistedState == null)
                return;

            p_hmData.StartDate = m_startTime.ToString("yyyy-MM-dd'T'HH:mm:ssZ");
            p_hmData.DailyEmailReportEnabled = m_persistedState.IsDailyEmailReportEnabled;

            p_hmData.RtpsTimerEnabled = m_persistedState.IsRealtimePriceServiceTimerEnabled;
            p_hmData.RtpsTimerFrequencyMinutes = cRtpsTimerFrequencyMinutes;

            var rtpsLastDownloadsSnapshot = m_rtpsLastDownloads.ToArray(); // we have to make a snapshot anyway, so the Timer thread can write it while we itarate
            int nDownloadsToReport = (rtpsLastDownloadsSnapshot.Length > 3) ? 3 : rtpsLastDownloadsSnapshot.Length;
            p_hmData.RtpsDownloads = new List<string>(nDownloadsToReport);
            for (int i = nDownloadsToReport; i > 0; i--)    // we need the last elements
            {
                var download = rtpsLastDownloadsSnapshot[rtpsLastDownloadsSnapshot.Length - i];
                p_hmData.RtpsDownloads.Add(download.Item1.ToString("yyyy -MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " : " + (download.Item2 ? "OK" : "ERROR"));
            }

            p_hmData.ProcessingVBrokerMessagesEnabled = m_persistedState.IsProcessingVBrokerMessagesEnabled;

            p_hmData.VBrokerReports = new List<string>();
            p_hmData.VBrokerDetailedReports = new List<string>();
            lock (m_VbReport)
            {
                int nReportsToReport = (m_VbReport.Count > 10) ? 10 : m_VbReport.Count;
                for (int i = nReportsToReport; i > 0; i--)
                {
                    var report = m_VbReport[m_VbReport.Count - i];    // Item3 is the whole message, but don't report it; the Dashboard should be brief
                    p_hmData.VBrokerReports.Add(report.Item1.ToString("yyyy -MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " : " + (report.Item2 ? "OK" : "ERROR"));
                    p_hmData.VBrokerDetailedReports.Add(report.Item4);
                }
            }

            p_hmData.VBrokerDetailedReports.Reverse();      // to see the latest item first
        }
    }
}
