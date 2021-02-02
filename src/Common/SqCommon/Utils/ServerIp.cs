using System;

namespace SqCommon
{
    public enum VBrokerServer {
        AutoVb, ManualVb
    }
    // gather IPs here so if it changes, it has to be changed only here
    public static class ServerIp
    {
        // DEV server: private IP: 172.31.60.145, public static IP (Elastic): 23.20.243.199 == currently http://snifferquant.net/ but what if in the future, the Website and HealthMonitor will be on separate servers. So, use IP, instead of DNS name *.net.
        public static string HealthMonitorListenerPrivateIp // HealthMonitor.exe thinks that is its IP
        {
            get
            {
                if (Utils.RunningPlatform() == Platform.Windows)
                    return "127.0.0.1";
                else
                    return "172.31.60.145";     // private IP of the VBrokerDEV server (where the HealthMonitor App runs)
            }
        }

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

        public static string HQaVM1PublicIp
        {
            get
            {
                if (Utils.RunningPlatform() == Platform.Windows)
                    return "localhost";       // sometimes for clients running on Windows (in development), we want localHost if Testing new HealthMonitor features
                                              //return "191.237.218.153";      // public IP for the VBrokerDEV server, sometimes for clients running on Windows (in development), we want the proper Healthmonitor if Testing runnig VBroker locally
                else
                    return "191.237.218.153";
            }
        }

        // VBroker server: private IP: 172.31.56.196, public IP (Elastic): 52.203.240.30
        public static string VirtualBrokerServerPrivateIpForListener
        {
            get
            {
                if (Utils.RunningPlatform() == Platform.Windows)
                    return "127.0.0.1";
                else
                {
                    throw new NotImplementedException();
                    // var vbServerEnvironment = Utils.Configuration["VbServerEnvironment"];
                    // if (vbServerEnvironment.ToLower() == "AutoTradingServer".ToLower())
                    //     return "172.31.56.196";     // private IP of the VBrokerAgent Linux (where VBrokerAgen app runs)
                    // else if (vbServerEnvironment.ToLower() == "ManualTradingServer".ToLower())
                    //     return "172.31.43.137";     // private IP of the VBrokerAgent Linux (where VBrokerAgen app runs)
                    // else
                    //     return "127.0.0.1";
                }
            }
        }

        public static string AtsVirtualBrokerServerPublicIpForClients   // AutoTraderServer
        {
            get {
                if (Utils.RunningPlatform() == Platform.Windows)
                    //return "localhost";       // sometimes for clients running on Windows (in development), we want localHost if Testing new VirtualBroker features
                    return "52.203.240.30";      // sometimes for clients running on Windows (in development), we want the proper VirtualBroker if Testing runnig VBroker locally
                else
                    return "52.203.240.30";
            }
        }

        public static string MtsVirtualBrokerServerPublicIpForClients   //ManualTraderServer
        {
            get
            {
                if (Utils.RunningPlatform() == Platform.Windows)
                    //return "localhost";       // for Debug: sometimes for clients running on Windows (in development), we want localHost VirtualBroker connection
                    return "34.251.1.119";      // sometimes for clients running on Windows (in development), we want the proper VirtualBroker if Testing runnig VBroker locally
                else
                    return "34.251.1.119";
            }
        }

        //public static string MtsVirtualBrokerServerPrivateIpForClients   //ManualTraderServer 
        public static string StandardLocalhostWithIP   // "127.0.0.1" is better: equals to "localhost" without costly DNS name resolution
        {
            // Connection from WebServer To Local (ManualTrader);
            // 127.0.0.1 is better than the private IP of the server, because that changes every time the AWS VM stopped/restarted.
            // Future speed improvement: This functions should give back not strings, but IPAddress, that is already resolved. The DNS resolution of "localhost" string to IPAddress is only done once, here.
            get
            {
                    return "127.0.0.1"; // At first, 127.0.0.1 didn't work on Linux, only the private IP of the Linux server worked. But that changes at every VM stop.
            }
        }

        public const int DefaultVirtualBrokerServerPort = 52101;    // largest port number: 65535, HealthMonitor listens on 52100, VBroker on 52101

    }
}
