using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;

namespace SqCommon
{
    public static partial class Utils
    {
        public static void TcpClientDispose(TcpClient? p_tcpClient)
        {
            if (p_tcpClient == null)
                return;
            p_tcpClient.Dispose();
        }
    }
}