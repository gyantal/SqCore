using System;
using SqCommon;

namespace SqCoreWeb
{
    public partial class Program
    {
        public static  void Services_Init()
        {
            Caretaker.g_caretaker.Init("SqCoreServer", Utils.Configuration["Emails:ServiceSupervisors"], p_needDailyMaintenance: true, TimeSpan.FromHours(2));

            Overmind.gOvermind.Init();
            WebsitesMonitor.gWebsitesMonitor.Init();
            BrAccChecker.gBrAccChecker.Init();
        }
        

        public static  void Services_Exit()
        {
            BrAccChecker.gBrAccChecker.Exit();
            WebsitesMonitor.gWebsitesMonitor.Exit();
            Overmind.gOvermind.Exit();

            Caretaker.g_caretaker.Exit();
        }

    }

}