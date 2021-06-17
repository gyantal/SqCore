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
            BrPrtfChecker.gBrPrtfChecker.Init();
        }
        

        public static  void Services_Exit()
        {
            BrPrtfChecker.gBrPrtfChecker.Exit();
            WebsitesMonitor.gWebsitesMonitor.Exit();
            Overmind.gOvermind.Exit();
        }

    }

}