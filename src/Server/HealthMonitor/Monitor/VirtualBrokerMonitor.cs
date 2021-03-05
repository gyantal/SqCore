using SqCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HealthMonitor
{
    public partial class HealthMonitor
    {
        Object m_lastVbInformSupervisorLock = new Object();   // null value cannot be locked, so we have to create an object
        DateTime m_lastVbErrorInformTime = DateTime.MinValue;    // don't email if it was made in the last 10 minutes
        // DateTime m_lastVbErrorPhoneCallTime = DateTime.MinValue;    // don't call if it was made in the last 30 minutes
        List<Tuple<DateTime, bool, string, string>> m_VbReport = new List<Tuple<DateTime, bool, string, string>>(); // List<> is not thread safe: <Date, IsOk, BriefReport, DetailedReport>

        // this is called every time the VirtualBroker send OK or Error: after every simulated trading
        // 1. General Error message looks like this. No HTML. Not Strategy (UberVXX, HarryLong) specific. VBroker can crash anywhere, without any strategy affiliation.
        // Message ID:"ReportErrorFromVirtualBroker", ParamStr: "Exception in BrokerTaskExecutionThreadRun Exception: . Exception: 'System.Exception: StrongAssert failed (severity==ThrowException): There is no point continuing if portfolioUSdSize cannot be calculated. After that we cannot calculate new stock Volumes from weights.
        //at SqCommon.StrongAssert.Fail_core(Severity p_severity, String p_message, Object[] p_args) in /home/ubuntu/SQ/Server/VirtualBroker/src/SQLab.Common/Utils/StrongAssert.cs:line 127
        // at VirtualBroke...'", ResponseFormat: "None"
        // 2. Strategy messages OK. Strategy specific. HTML format.
        // Message ID:"ReportOkFromVirtualBroker", ParamStr: "<BriefReport>BrokerTask HarryLong was OK.</BriefReport><DetailedReport>11-09T20:59:49: 'HarryLong'<br><font color="#10ff10">Target: TVIX:-35%, TMV:-65%</font><br/>Real SellAsset  6 UVXY ($82)<br/>Real BuyAsset  29 TMV ($626)<br/>Real BuyAsset  46 UVXY ($626)<br/>Real BuyAsset  581 TMV ($12541)<br/></DetailedReport>", ResponseFormat: "None"
        private void MessageFromVirtualBroker(TcpClient p_tcpClient, HealthMonitorMessage p_message)
        {
            Utils.Logger.Info($"MessageFromVirtualBroker() START");
            if (p_message.ResponseFormat == HealthMonitorMessageResponseFormat.String)
            {
                BinaryWriter bw = new BinaryWriter(p_tcpClient.GetStream());
                bw.Write("FromServer: Message received, saved and starting processing: " + p_message.ParamStr);
            }

            if (m_persistedState == null || !m_persistedState.IsProcessingVBrokerMessagesEnabled)
                return;

            //string healthMonitorMsg = $"<BriefReport>{briefReport}</BriefReport><DetailedReport>{detailedReportSb.ToString()}</DetailedReport>";
            // or in a case of VBroker crash, it is simply a HealthMonitorMessageID.ReportErrorFromVirtualBroker ID, with no "<BriefReport>" structure.
            string? briefReport = null, detailedReport = null;
            int briefReportBegin = p_message.ParamStr.IndexOf("<BriefReport>");
            if (briefReportBegin != -1)
            {
                int briefReportEnd = p_message.ParamStr.IndexOf("</BriefReport>", briefReportBegin + "<BriefReport>".Length);
                if (briefReportEnd != -1)
                {
                    int detailedReportBegin = p_message.ParamStr.IndexOf("<DetailedReport>", briefReportEnd + "</BriefReport>".Length);
                    if (detailedReportBegin != -1)
                    {
                        int detailedReportEnd = p_message.ParamStr.IndexOf("</DetailedReport>", briefReportBegin + "<DetailedReport>".Length);
                        if (detailedReportEnd != -1)
                        {
                            briefReport = p_message.ParamStr.Substring(briefReportBegin + "<BriefReport>".Length, briefReportEnd - briefReportBegin - "<BriefReport>".Length);
                            detailedReport = p_message.ParamStr.Substring(detailedReportBegin + "<DetailedReport>".Length, detailedReportEnd - detailedReportBegin - "<DetailedReport>".Length);
                        }
                    }
                }
            }

            bool isErrorOrWarning = (p_message.ID == HealthMonitorMessageID.ReportErrorFromVirtualBroker) || (p_message.ID == HealthMonitorMessageID.ReportWarningFromVirtualBroker);
            if (!isErrorOrWarning && (briefReport != null))
            {
                // sometimes the message seems OK, but if the messageParam contains the word "Error" treat it as error. For example, if this email was sent to the user, with the Message that everything is OK, treat it as error
                // "***Trade: ERROR"   + "*** StrongAssert failed (severity==Exception): BrokerAPI.GetStockMidPoint(...) failed"
                // "ibNet_ErrorMsg(). TickerID: 742, ErrorCode: 404, ErrorMessage: 'Order held while securities are located.'
                // "Error. A transaction was not executed. p_brokerAPI.GetExecutionData = null for Sell SPY Volume: 266. Check that it was not executed and if not, perform it manually then enter into the DB.
                isErrorOrWarning = (briefReport.IndexOf("Error", StringComparison.CurrentCultureIgnoreCase) != -1);  // in DotNetCore, there is no StringComparison.InvariantCultureIgnoreCase
            }

            lock (m_VbReport)
                m_VbReport.Add(new Tuple<DateTime, bool, string, string>(DateTime.UtcNow, !isErrorOrWarning, ((briefReport != null) ? briefReport : "ReportFromVirtualBroker without BriefReport"), ((detailedReport != null) ? detailedReport : p_message.ParamStr)));

            if (isErrorOrWarning)
            {
                Utils.Logger.Info("Error or Warning FromVirtualBroker().");
                // Vbroker source can spam error messages every 1 second. We don't want to spam email/phonecalls every second, therefore use urgency as Normal
                InformSupervisorsEx(DataSource.VBroker, false, "SQ HealthMonitor: ERROR from VirtualBroker.", $"SQ HealthMonitor: ERROR from VirtualBroker. MessageParamStr: { ((briefReport != null) ? briefReport : p_message.ParamStr) }", 
                    "There is an Error in Virtual Broker. ... I repeat: Error in Virtual Broker.", ref m_lastVbInformSupervisorLock, ref m_lastVbErrorInformTime);
            }

            Utils.Logger.Info($"MessageFromVirtualBroker() END");
        }

        public void CheckOKMessageArrived(DateTime p_utcStart, string p_triggeredTaskSchemaName) // p_triggeredTaskSchemaName = "UberVXX" or "HarryLong"
        {
            Utils.Logger.Debug("HmVb.CheckOKMessageArrived() BEGIN");
            Tuple<DateTime, bool, string, string>? expectedMessage = null;
            lock (m_VbReport)
            {
                for (int i = 0; i < m_VbReport.Count; i++)
                {
                    if (m_VbReport[i].Item1 > p_utcStart)
                    {
                        bool isOK = m_VbReport[i].Item2;
                        string strategyName = String.Empty;
                        int strategyNameInd1 = m_VbReport[i].Item3.IndexOf("BrokerTask ");  // "BrokerTask UberVXX/HarryLong was OK" or "had ERROR"
                        if (strategyNameInd1 != -1)
                        {
                            int strategyNameInd2 = strategyNameInd1 + "BrokerTask ".Length;
                            int strategyNameInd3 = m_VbReport[i].Item3.IndexOf(" ", strategyNameInd2);
                            if (strategyNameInd3 != -1)
                            {
                                strategyName = m_VbReport[i].Item3.Substring(strategyNameInd2, strategyNameInd3 - strategyNameInd2);
                                if (strategyName == p_triggeredTaskSchemaName)
                                {
                                    expectedMessage = m_VbReport[i];
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (expectedMessage == null)    // Send email, make phonecall
            {
                Utils.Logger.Debug("HmVb.CheckOKMessageArrived(): Message missing.");
                InformSupervisorsEx(DataSource.VBrokerCheckOKMessageArrived, false, $"SQ HealthMonitor: VirtualBroker Message from {p_triggeredTaskSchemaName} didn't arrive.", $"SQ HealthMonitor: VirtualBroker Message from {p_triggeredTaskSchemaName} did't arrive.", $"Virtual Broker message from from {p_triggeredTaskSchemaName} didn't arrive. ... I repeat: Virtual Broker message from from {p_triggeredTaskSchemaName} didn't arrive.", ref m_lastVbInformSupervisorLock, ref m_lastVbErrorInformTime);
            }
            else
            {
                Utils.Logger.Debug("HmVb.CheckOKMessageArrived(): OK.");
                // do nothing. The arrived message can be either an OK or Error message. But if it was an Error message, the Phonecall was already made when the Error message arrived
            }
        }
    }
}
