using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace SqCommon;

public static partial class Utils
{
    public static void TcpClientDispose(TcpClient? p_tcpClient)
    {
        if (p_tcpClient == null)
            return;
        p_tcpClient.Dispose();
    }

    public static void OpenInBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("^", "^^").Replace("&", "^&")}")); // The ^ symbol is the escape character* in Cmd.exe (for & \ < > ^ |)
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
}