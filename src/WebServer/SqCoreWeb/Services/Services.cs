using System;
using SqCommon;

namespace SqCoreWeb
{
    public partial class Program
    {
        public static  void Services_Init()
        {
            Overmind.gOvermind.Init();
            WebsitesMonitor.gWebsitesMonitor.Init();
            BrAccChecker.gBrAccChecker.Init();
        }
        

        public static  void Services_Exit()
        {
            BrAccChecker.gBrAccChecker.Exit();
            WebsitesMonitor.gWebsitesMonitor.Exit();
            Overmind.gOvermind.Exit();
        }

    }

}