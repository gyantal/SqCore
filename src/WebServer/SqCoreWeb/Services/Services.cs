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
        }
        

        public static  void Services_Exit()
        {
            WebsitesMonitor.gWebsitesMonitor.Exit();
            Overmind.gOvermind.Exit();
        }

    }

}