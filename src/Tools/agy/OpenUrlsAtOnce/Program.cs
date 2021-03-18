using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenUrlsAtOnce
{
    class Program
    {
        static void Main(string[] args)
        {
            int sleepInMsec = 300;       // 50 was not enough for digg.com and channel9, 100msec was not enough on 2012-07-18, 150mset was not enough on 2012-07-19

            OpenBrowser("https://www.wsj.com/"); Thread.Sleep(sleepInMsec);    // free speech: allow comments
            OpenBrowser("http://www.ft.com/home/europe"); Thread.Sleep(sleepInMsec);   // free speech: allow comments
            OpenBrowser("https://www.theguardian.com/uk?INTCMP=CE_UK"); Thread.Sleep(sleepInMsec); // 2018-05: Changed from Independent to TheGuardian, because it has 3x as much circulation, but I don't like it because too leftist (instead of liberal), and doesn't allow free speach, no comments

            // OpenBrowser("http://444.hu/"); Thread.Sleep(sleepInMsec);  // After index.hu fiasco, everybody went to 444, but it is only politics, it doesn't contain Tech or Viral news.
            // OpenBrowser("https://telex.hu"); Thread.Sleep(sleepInMsec);    // the old Index authors went to this. But later I found it is too socialist: write about 'virus-tagadok', etc. discontinue.
            OpenBrowser("https://24.hu/"); Thread.Sleep(sleepInMsec);  // this is more like Index.hu portal. Has a small Tech/VIP persons section
            OpenBrowser("http://www.gamekapocs.hu/PC"); Thread.Sleep(sleepInMsec);

            OpenBrowser("http://www.napi.hu/"); Thread.Sleep(sleepInMsec);
            OpenBrowser("http://prog.hu/"); Thread.Sleep(sleepInMsec);
            OpenBrowser("http://www.hwsw.hu/"); Thread.Sleep(sleepInMsec);
            OpenBrowser("http://www.sciencedaily.com/news"); Thread.Sleep(sleepInMsec);
            
            if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday)
                OpenBrowser("https://channel9.msdn.com/Shows/This+Week+On+Channel+9"); Thread.Sleep(sleepInMsec);

            OpenBrowser("http://stockcharts.com/h-sc/ui?s=$NYMO"); Thread.Sleep(sleepInMsec);  // McClellan Oscillator
            OpenBrowser("http://stockcharts.com/h-sc/ui?s=QQQ:SPY&p=D&b=5&g=0&id=p60129699268"); Thread.Sleep(sleepInMsec);
            OpenBrowser("http://stockcharts.com/h-sc/ui?s=VXX"); Thread.Sleep(sleepInMsec);
            OpenBrowser("http://stockcharts.com/h-sc/ui?s=SPX"); Thread.Sleep(sleepInMsec);
            OpenBrowser("http://finance.yahoo.com/echarts?s=^VIX#symbol=%5Evix;range=5d;compare=;indicator=volume;charttype=line;crosshair=on;ohlcvalues=0;logscale=on;source=;"); Thread.Sleep(sleepInMsec);

            OpenBrowser("https://www.dailyfx.com/eur-usd"); Thread.Sleep(sleepInMsec);
            OpenBrowser("https://www.dailyfx.com/gbp-usd"); Thread.Sleep(sleepInMsec);
            OpenBrowser("https://www.poundsterlinglive.com/data/currencies/gbp-pairs/GBPHUF-exchange-rate/"); Thread.Sleep(sleepInMsec);
        }

        public static void OpenBrowser(string url)
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
}
