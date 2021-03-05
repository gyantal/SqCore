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
        Object m_lastSqWebsiteInformSupervisorLock = new Object();   // null value cannot be locked, so we have to create an object
        DateTime m_lastSqWebsiteErrorEmailTime = DateTime.MinValue;    // don't email if it was made in the last 10 minutes
        DateTime m_lastSqWebsiteErrorPhoneCallTime = DateTime.MinValue;    // don't call if it was made in the last 30 minutes
      
        private void ErrorFromWebsite(TcpClient p_tcpClient, HealthMonitorMessage p_message)
        {
            if (m_persistedState == null || !m_persistedState.IsProcessingSqCoreWebsiteMessagesEnabled)
                return;

            if (p_message.ResponseFormat == HealthMonitorMessageResponseFormat.String)
            {
                BinaryWriter bw = new BinaryWriter(p_tcpClient.GetStream());
                bw.Write("FromServer: Message received, saved and starting processing: " + p_message.ParamStr);
            }

            string from = "unknown website";
            switch (p_message.ID)
            {
                case HealthMonitorMessageID.ReportErrorFromSQLabWebsite:
                    from = "SqLab";
                    break;
                case HealthMonitorMessageID.SqCoreWebCsError:
                    from = "SqCore.C#";
                    break;
                case HealthMonitorMessageID.SqCoreWebJsError:
                    from = "SqCore.Javascript";
                    break;
             }

            Utils.Logger.Info("ErrorFromWebsite().");

            InformSupervisors(InformSuperVisorsUrgency.Normal_UseTimer, $"SQ HealthMonitor: ERROR. Website: {from}.", $"SQ HealthMonitor:ERROR. Website: {from}. MessageParamStr: { p_message.ParamStr}", String.Empty, ref m_lastSqWebsiteInformSupervisorLock, ref m_lastSqWebsiteErrorEmailTime, ref m_lastSqWebsiteErrorPhoneCallTime);
        }

    
        
    }
}
