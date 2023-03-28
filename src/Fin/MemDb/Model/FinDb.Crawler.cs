using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SqCommon;
using YahooFinanceApi;

namespace Fin.MemDb;

public partial class FinDb
{
    public static async Task<StringBuilder> CrawlData(bool p_isLogHtml) // print log to Console or HTML
    {
        StringBuilder logSb = new();
        List<string> tickers = new() { "SPY" };

        for (int i = 0; i < tickers.Count; i++)
        {
            string ticker = tickers[i];
            bool isOK = await CrawlData(ticker, p_isLogHtml, logSb);
            if (!isOK)
                logSb.AppendLine($"Error processing {ticker}");
        }
        return logSb;
    }

    // ToDo:
    // create the CSV next to the SPY.ZIP
    // how to create ZIP from CSV
    // before overwriting the ZIP, file archive handling. Maybe IB "-Fri" renames.
    // factor file with dividends, split
    // not only SPY, but testing others.
    // List of tickers should come from the map_files folder.

    // next with George: Daily running as service

    public static async Task<bool> CrawlData(string p_ticker, bool p_isLogHtml, StringBuilder p_logSb)
    {
        Console.WriteLine($"FinDb.CrawlData() START with ticker: {p_ticker}");

        IReadOnlyList<Candle?>? history = await Yahoo.GetHistoricalAsync(p_ticker, null, null, Period.Daily); // if asked 2010-01-01 (Friday), the first data returned is 2010-01-04, which is next Monday. So, ask YF 1 day before the intended
        if (history == null)
        {
            if (p_isLogHtml)
                p_logSb.AppendLine($"Cannot download YF data (ticker:{p_ticker}) after many tries.</br>");
            else
                p_logSb.AppendLine($"Cannot download YF data (ticker:{p_ticker}) after many tries.");
            return false;
        }
        if (history.Count > 0 && history[0] != null)
            Console.WriteLine($"First candle: {history[0]?.DateTime:yyyy-MM-dd}");

        // Windows: AppDomain.BaseDir: D:\GitHub\SqCore\src\WebServer\SqCoreWeb\bin\Debug\net7.0\
        // Linux: AppDomain.BaseDir: /home/sq-vnc-client/SQ/WebServer/SqCoreWeb/published/publish/
        // FinDataDir directory: Windows: d:\GitHub\SqCore\src\Fin\Data ; Linux: ~/SQ/WebServer/SqCoreWeb/published/FinData
        string finDataDir = OperatingSystem.IsWindows() ?
            AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\..\..\Fin\Data\" :
            AppDomain.CurrentDomain.BaseDirectory + @"../FinData/";
        finDataDir = Path.GetFullPath(finDataDir); // GetFullPath removes the unnecessary back marching ".."

        string tickerHistFilePath = finDataDir + $"{p_ticker}.csv";
        TextWriter tw = new StreamWriter(tickerHistFilePath);
        tw.WriteLine("The next line!");
        tw.Close();

        return true;
    }
}