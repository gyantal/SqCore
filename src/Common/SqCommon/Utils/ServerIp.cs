using System;

namespace SqCommon
{
    public enum VBrokerServer {
        AutoVb, ManualVb
    }
    // gather IPs here so if it changes, it has to be changed only here
    public static class ServerIp
    {
        // Obsolete: probably delete this HealthMonitorListenerPrivateIp, because HealthMonitor listens on 0.0.0.0 on all localhost + private IPs
        // DEV server: private IP: 172.31.60.145, public static IP (Elastic): 23.20.243.199 == currently http://snifferquant.net/ but what if in the future, the Website and HealthMonitor will be on separate servers. So, use IP, instead of DNS name *.net.
        // public static string HealthMonitorListenerPrivateIp // HealthMonitor.exe thinks that is its IP
        // {
        //     get
        //     {
        //         if (Utils.RunningPlatform() == Platform.Windows)
        //             return "127.0.0.1";
        //         else
        //             return "172.31.60.145";     // private IP of the VBrokerDEV server (where the HealthMonitor App runs)
        //     }
        // }

        public static string HealthMonitorPublicIp      // for Clients. Clients of HealthMonitor sees this
        {
            get
            {
                if (Utils.RunningPlatform() == Platform.Windows)
                    //return "localhost";       // sometimes for clients running on Windows (in development), we want localHost if Testing new HealthMonitor features
                    return "23.20.243.199";      // public IP for the VBrokerDEV server, sometimes for clients running on Windows (in development), we want the proper Healthmonitor if Testing runnig VBroker locally
                else
                    return "23.20.243.199";
            }
        }

        public static string AtsVirtualBrokerServerPublicIpForClients   // AutoTraderServer
        {
            get
            {
                return "52.203.240.30";
            }
        }

        public static string SqCoreServerPublicIpForClients   //ManualTraderServer
        {
            get
            {
                return "34.251.1.119";
            }
        }

        // Difference between 127.0.0.1 and 0.0.0.0
        // https://www.howtogeek.com/225487/what-is-the-difference-between-127.0.0.1-and-0.0.0.0
        // netstat -lntp  // show listener ports
        // Future speed improvement: This functions should give back not strings, but IPAddress, that is already resolved. The DNS resolution of "localhost" string to IPAddress is only done once, here.
        public static string LocalhostLoopbackWithIP   // "127.0.0.1" is better: equals to "localhost" without costly DNS name resolution
        {
            // For example: Connection from WebServer To Local (ManualTrader);
            // 127.0.0.1 is better than the private IP of the server, because that changes every time the AWS VM stopped/restarted.
            get
            {
                    return "127.0.0.1"; // loopback address (works even if machine has no network card); in Debug when you test the service and want to achieve it from the local machine
            }
        }

        
        public static string LocalhostMetaAllPrivateIpWithIP // 0.0.0.0 means all IPv4 addresses on the local machine (equals LocalHost_127.0.0.1 Plus all private IPs of the computer), cannot be used for target, only for listening
        {
            get
            {
                    return "0.0.0.0"; // use 0.0.0.0 in Production when you want that it is accessible from the Internet (binding to all local private IPs) 
            }
        }

        public const int DefaultHealthMonitorServerPort = 52100;    // largest port number: 65535, HealthMonitor listens on 52100, VBroker on 52101
        public const int DefaultVirtualBrokerServerPort = 52101;    // largest port number: 65535, HealthMonitor listens on 52100, VBroker on 52101

    }
}
