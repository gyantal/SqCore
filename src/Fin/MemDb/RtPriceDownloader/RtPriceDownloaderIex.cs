using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqCommon;

namespace Fin.MemDb;

// ***************************************************
// IEX specific only
// https://cloud.iexapis.com/stable/tops?token=pk_281c0e3abdef4f6f9fbf917c6d6e67af&symbols=QQQ,SPY   just a short price data, takes about 150ms, so it is quite fast.
// Tops query: max allowed is 1028 tickers in the query. If 1029 are asked => ERR_CONNECTION_CLOSED exception. Sometimes that threshold is 1000, so about 900 is 'safe'
// https://cloud.iexapis.com/stable/stock/market/batch?symbols=QQQ&types=quote&token=<...>  a bigger data,, takes about 250ms.
// Market query: if symbols are more than 100, it returns only 100.
// >08:20: "previousClose":215.37, !!! that is wrong. So IEX, next day at 8:20, it still gives back PreviousClose as 2 days ago. Wrong., ""latestPrice":216.48, "latestSource":"Close","latestUpdate":1582750800386," is the correct one, "iexRealtimePrice":216.44 is the 1 second earlier.
// >09:32: "previousClose":215.37  (still wrong), ""latestPrice":216.48, "latestSource":"Close","latestUpdate":1582750800386," is the correct one, "iexRealtimePrice":216.44 is the 1 second earlier.
// >10:12: "previousClose":215.37  (still wrong), "close":216.48,"closeTime":1582750800386  // That 'close' is correct, but previousClose is not.
// >11:22: "previousClose":215.37  (still wrong), "close":null,"closeTime":null   // 'close' is nulled
// >12:22: "previousClose":215.37  (still wrong), "close":null,"closeTime":null
// >14:15: "previousClose":215.37, "latestPrice":216.48,"latestSource":"Close",  (still wrong), just 15 minutes before market open, it is still wrong., "close":null,"closeTime":null
// >14:59: "previousClose":216.48, (finally correct) "close":null, "latestPrice":211.45,"latestSource":"IEX real time price","latestTime":"9:59:26 AM", so they fixed it only after the market opened at 14:30. It also reveals that they don't do Pre-market price, which is important for us.
// >21:50: "previousClose":216.48, "close":null,"closeTime":null, "latestPrice":205.82,"latestSource":"IEX price","latestTime":"3:59:56 PM",
// which is bad. The today Close price at 21:00 was 205.64, but it is not in the text anywhere. prevClose is 2 days ago, latestPrice is the 1 second early, not the ClosePrice.
// https://cloud.iexapis.com/stable/stock/market/batch?symbols=QQQ&types=chart&token=<...> 'chart': last 30 days data per day:
// https://cloud.iexapis.com/stable/stock/market/batch?symbols=QQQ&types=previous&token=<...>   'previous':
// >"// Paid account: $9 per month per 5 million messages/mo: 5000000/30/20/60 = 138 messages per minute."
// --------------------------------------------------------------
// ------------------ Problems of IEX:
// - pre/Postmarket only: 8am-9:30am and 4pm-5pm, when Yahoo has it from 9:00 UTC. So, it is not enough.
// - cut-off time is too late. Until 14:30 asking PreviousDay, it still gives the price 2 days ago. When YF will have premarket data at 9:00. Although "latestPrice" can be used as close.
// - the only good thing: in market-hours, RT prices are free (max 50K queries per month), and very quick to obtain and batched.

// for Vbroker trading, we will use IB streaming data. No frequency timer is required. It is streamed directly.
// use IEX only for High/Mid Freq, and only in RegularTrading. So, High/MidFreq OTH uses YF. But use it sparingly, so YF doesn't ban us.
// IEX: free account: 50000/30/8/60/60= 3.5. We can do max 3 queries per minute with 1 user-token. But we can use 2 tokens. Just to be on the safe side:
// 2023-06-12: because YF realtime didn't work, we used IEX Market, and somehow we used up the monthly quota in 12 days. Maybe they reduced the 50,000 per month free quota in 2023
// For RT highFreq: use 30 seconds, but alternade the 2 tokens we use. That will be about 1 query per minute per token = 60*8*30 = 15K queries per token per month. Although Developers also use some of the quota while developing.
// Is there a need for 2 IEX timers? (High/Mid Freq) MidFreq timer can be deleted. Questionable, but keep this logic! In the future, we might use a 3rd RT service.
public class RtPriceDownloaderIex
{
    byte m_lastIexApiTokenInd = 2; // possible values: { 1, 2}. Alternate 2 API tokens to stay bellow the 50K quota. Token1 is the hedgequantserver, Token2 is the UnknownUser.
    uint m_nDownload = 0;

    public uint NumDownload { get => m_nDownload; }

    // compared to IB data stream, IEX is sometimes 5-10 sec late. But sometimes it is not totally accurate. It is like IB updates its price every second. IEX updates randomly. Sometimes it updates every 1 second, sometime after 10seconds. In general this is fine.
    // "We limit requests to 100 per second per IP measured in milliseconds, so no more than 1 request per 10 milliseconds."
    // https://iexcloud.io/pricing/
    // Free account: 50,000 core messages/mo, That is 50000/30/20/60 = 1.4 message per minute.
    // Paid account: $9 per 5 million messages/mo: 5000000/30/20/60 = 134 messages per minute.
    // PreviousClose data: https://cloud.iexapis.com/stable/stock/market/batch?symbols=AAPL,FB&types=quote&token=<get it from sensitive-data file>
    public async Task DownloadLastPrice(Asset[] p_assets) // takes 450-540ms from WinPC
    {
        Utils.Logger.Debug("DownloadLastPriceIex() START");
        m_nDownload++;
        try
        {
            string? iexApiToken = GetAndRotateNextApiToken();

            // TODO: max allowed is 1028 tickers in the query. If 1029 are asked => ERR_CONNECTION_CLOSED exception. So, we have to do it in a second query
            // TODO: check if IEX can handle Index assets. Only Stock has IexTicker
            string[]? iexTickers = p_assets.Where(r => r is Stock).Select(r => (r as Stock)!.IexTicker).Take(800).ToArray(); // treat similarly as DownloadLastPriceYF()
            string url = $"https://cloud.iexapis.com/stable/tops?token={iexApiToken}&symbols={String.Join(",", iexTickers)}";
            string? responseStr = await Utils.DownloadStringWithRetryAsync(url);
            if (responseStr == null)
                return;

            Utils.Logger.Debug("DownloadLastPriceIex() str = '{0}'", responseStr);
            ExtractAttributeIexFromTops(responseStr, "lastSalePrice", p_assets);
        }
        catch (Exception e)
        {
            Utils.Logger.Error(e, "DownloadLastPriceIex()");
        }
    }

    public async Task DownloadPriorCloseAndLastPrice(Asset[] p_assets)
    {
        Utils.Logger.Debug("DownloadPriorCloseAndLastPriceIex() START");
        m_nDownload++;
        try
        {
            string? iexApiToken = GetAndRotateNextApiToken();

            // TODO: check if IEX can handle Index assets. Only Stock has IexTicker
            // string[]? iexTickers = p_assets.Where(r => r is Stock).Select(r => (r as Stock)!.IexTicker).Take(800).ToArray(); // treat similarly as DownloadLastPriceYF()
            // split into arrays of no more than 100, because for Market query: if symbols are more than 100, it returns only 100.
            var assetsChunks = p_assets.Where(r => r is Stock).Chunk(100);
            foreach (var assetsChunk in assetsChunks)
            {
                string[]? iexTickers = assetsChunk.Select(r => (r as Stock)!.IexTicker).ToArray();
                string url = $"https://cloud.iexapis.com/stable/stock/market/batch?types=quote&token={iexApiToken}&symbols={String.Join(",", iexTickers)}";
                // string url = $"https://cloud.iexapis.com/stable/stock/market/batch?types=quote&token={iexApiToken}&symbols=TQQQ";
                string? responseStr = await Utils.DownloadStringWithRetryAsync(url);
                if (responseStr == null)
                    return;
                if (responseStr.Length <= "Access is restricted to paid subscribers. Please upgrade to gain access".Length)
                    Console.WriteLine($"DownloadPriorCloseAndLastPriceIex() unexpected response: '{responseStr}'");

                Utils.Logger.Debug("DownloadPriorCloseAndLastPriceIex() str = '{0}'", responseStr);
                ExtractAttributeIexFromMarketData(responseStr, "latestPrice", assetsChunk);
                ExtractAttributeIexFromMarketData(responseStr, "previousClose", assetsChunk);
            }
        }
        catch (Exception e)
        {
            Utils.Logger.Error(e, "DownloadPriorCloseAndLastPriceIex()");
        }
    }

    private string? GetAndRotateNextApiToken()
    {
        var iexApiToken = Utils.Configuration[$"Iex:ApiToken{m_lastIexApiTokenInd}"];
        if (m_lastIexApiTokenInd < 2)
            m_lastIexApiTokenInd++;
        else
            m_lastIexApiTokenInd = 1;
        return iexApiToken;
    }

    private static void ExtractAttributeIexFromMarketData(string p_responseStr, string p_attribute, Asset[] p_assets) // Tops is different. Order is: latestPrice/previousClose/symbol in a record
    {
        List<string> zeroValueSymbols = new();
        List<string> properlyArrivedSymbols = new();
        int iStr = 0;   // this is the fastest. With IndexOf(). Not using RegEx, which is slow.
        while (iStr < p_responseStr.Length)
        {
            int bAttribute = p_responseStr.IndexOf(p_attribute + "\":", iStr);
            if (bAttribute == -1)
                break;
            bAttribute += (p_attribute + "\":").Length;
            int eAttribute = p_responseStr.IndexOf(",\"", bAttribute);
            if (eAttribute == -1)
                break;
            string attributeStr = p_responseStr[bAttribute..eAttribute];

            int bSymbol = p_responseStr.IndexOf("symbol\":\"", eAttribute);
            if (bSymbol == -1)
                break;
            bSymbol += "symbol\":\"".Length;
            int eSymbol = p_responseStr.IndexOf("\"", bSymbol);
            if (eSymbol == -1)
                break;
            string iexTicker = p_responseStr[bSymbol..eSymbol];
            if (iexTicker == "UNG")
                Console.WriteLine($"IEX: {iexTicker}, attributeStr: {attributeStr}");

            // only search ticker among the stocks p_assetIds. Because duplicate tickers are possible in the MemDb.Assets, but not expected in p_assetIds
            Stock? stock = null;
            foreach (var sec in p_assets)
            {
                if (sec is not Stock iStock)
                    continue;
                if (iStock.IexTicker == iexTicker)
                {
                    stock = iStock;
                    break;
                }
            }

            if (stock != null)
            {
                bool isConvertedOK = float.TryParse(attributeStr, out float attribute);

                if (!isConvertedOK || attribute == 0.0f)
                    zeroValueSymbols.Add(stock.SqTicker);
                else // don't overwrite the MemDb data with false 0.0 values.
                {
                    properlyArrivedSymbols.Add(stock.SqTicker); // have to use unique SqTicker, coz Symbol = "VOD" for both "S/VOD" and "S/VOD.L"
                    switch (p_attribute)
                    {
                        case "previousClose":
                            stock.PriorClose = attribute;
                            break;
                        case "lastSalePrice": // in https://cloud.iexapis.com/stable/tops queries only
                        case "latestPrice":
                            stock.EstValue = attribute;
                            break;
                    }
                }
            }
            iStr = eSymbol;
        }

        if (properlyArrivedSymbols.Count != p_assets.Length)
        {
            var missing = p_assets.Where(r => !properlyArrivedSymbols.Contains(r.SqTicker)).ToList();
            var msg = $"IEX RT price extract for {p_attribute}: Ok({properlyArrivedSymbols.Count})<Queried({p_assets.Length}). Missing:{String.Join(',', missing.Select(r => r.SqTicker))}";
            SqConsole.WriteLine(msg);
            Utils.Logger.Warn(msg);
        }

        if (zeroValueSymbols.Count != 0)
            Utils.Logger.Warn($"ExtractAttributeIex() zero lastPrice values: {String.Join(',', zeroValueSymbols)}");
    }

    private static void ExtractAttributeIexFromTops(string p_responseStr, string p_attribute, Asset[] p_assets) // Order is: symbol/lastSalePrice in a record
    {
        List<string> zeroValueSymbols = new();
        List<string> properlyArrivedSymbols = new();
        int iStr = 0;   // this is the fastest. With IndexOf(). Not using RegEx, which is slow.
        while (iStr < p_responseStr.Length)
        {
            int bSymbol = p_responseStr.IndexOf("symbol\":\"", iStr);
            if (bSymbol == -1)
                break;
            bSymbol += "symbol\":\"".Length;
            int eSymbol = p_responseStr.IndexOf("\"", bSymbol);
            if (eSymbol == -1)
                break;
            string iexTicker = p_responseStr[bSymbol..eSymbol];
            int bAttribute = p_responseStr.IndexOf(p_attribute + "\":", eSymbol);
            if (bAttribute == -1)
                break;
            bAttribute += (p_attribute + "\":").Length;
            int eAttribute = p_responseStr.IndexOf(",\"", bAttribute);
            if (eAttribute == -1)
                break;
            string attributeStr = p_responseStr[bAttribute..eAttribute];

            if (iexTicker == "UNG")
                Console.WriteLine($"IEX: {iexTicker}, attributeStr: {attributeStr}");
            // only search ticker among the stocks p_assetIds. Because duplicate tickers are possible in the MemDb.Assets, but not expected in p_assetIds
            Stock? stock = null;
            foreach (var sec in p_assets)
            {
                if (sec is not Stock iStock)
                    continue;
                if (iStock.IexTicker == iexTicker)
                {
                    // if (iexTicker == "TQQQ")
                    //     Console.WriteLine("TQQQ in IEX");
                    stock = iStock;
                    break;
                }
            }

            if (stock != null)
            {
                bool isConvertedOK = float.TryParse(attributeStr, out float attribute);

                if (!isConvertedOK || attribute == 0.0f) // For even popular stocks (IDX,AFK,ONVO,BIB,VXZ) Tops returns a record but with "lastSalePrice":0. Even during market hours. Consider these invalid and don't update our RT price.
                    zeroValueSymbols.Add(stock.SqTicker);
                else // don't overwrite the MemDb data with false 0.0 values.
                {
                    properlyArrivedSymbols.Add(stock.SqTicker); // have to use unique SqTicker, coz Symbol = "VOD" for both "S/VOD" and "S/VOD.L"
                    switch (p_attribute)
                    {
                        case "previousClose":
                            stock.PriorClose = attribute;
                            break;
                        case "lastSalePrice": // in https://cloud.iexapis.com/stable/tops queries only
                        case "latestPrice":
                            stock.EstValue = attribute;
                            break;
                    }
                }
            }
            iStr = eAttribute;
        }

        if (properlyArrivedSymbols.Count != p_assets.Length)
        {
            var missing = p_assets.Where(r => !properlyArrivedSymbols.Contains(r.SqTicker)).ToList();
            var msg = $"IEX RT price: Ok({properlyArrivedSymbols.Count})<Queried({p_assets.Length}). Missing:{String.Join(',', missing.Select(r => r.SqTicker))}";
            SqConsole.WriteLine(msg);
            Utils.Logger.Warn(msg);
        }

        if (zeroValueSymbols.Count != 0)
            Utils.Logger.Warn($"ExtractAttributeIex() zero lastPrice values: {String.Join(',', zeroValueSymbols)}");
    }
}