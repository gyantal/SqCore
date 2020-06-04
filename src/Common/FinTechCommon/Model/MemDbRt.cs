using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using SqCommon;
using YahooFinanceApi;

namespace FinTechCommon
{
    // many functions might use RT price, so it should be part of the MemDb
    public partial class MemDb
    {
        // ***** Plan: Historical and RT quote data
        // - the Website.app, once a day, gets historical price data from YF. Get all history.
        // - for RT price: during market-hours use IEX 'top', because it is free (and YF might ban our IP if we query too many times, IB has rate limit per minute, and VBroker need that bandwidth)
        // - pre/postmarket, also use YF, but with very-very low frequency. Once per every 5 sec. (only if any user is connected). We don't need to use IB Markprice, because YF pre-market is very quickly available at 9:00, 5.5h before open.
        // - code should know whether it is pre/postmarket hours, so we have to implement the same logic as in VBroker. (with the holiday days, and DB).
        // - Call AfterMarket = PostMarket, because shorter and tradingview.com also calls "post-market" (YF calls: After hours)

        // ***************************************************
        // YF, IB, IEX preMarket, postMarket behaviour
        // >UTC-7:50: YF:  no pre-market price. IB: there is no ask-bid, but there is an indicative value somehow, because Dashboard bar shows QQQ: 216.13 (+0.25%). I checked, that is the 'Mark price'. IB estimates that (probably for pre-market margin calls). YF and others will never have that, because it is sophisticated.
        // >UTC-8:50: YF:  no pre-market price. IB: there is no ask-bid, but previously good indicative value went back to PreviousClose, so ChgPct = 0%, so in PreMarket that far away from Open, this MarkPrice is not very useful. IB did reset the price, because preparing for pre-market open in 10min.
        // >UTC-9:10: YF (started at 9:00, there is premarket price: "Pre-Market: 4:03AM EST"), IB: There is Ask-bid spread. This is 5.5h before market open. That should be enough. So, I don't need IB indicative MarkPrice. IB AccInfo website is also good, showing QQQ change. IEX: IEX shows some false data, which is not yesterday close, but probably last day postMarket lastPrice sometime, which is not useful. So, in pre-market this IEX 'top' cannot be used. But maybe IEX cloud can be used in pre-market. Investigate.
        // It is important here, because of summer/winter time zones that IB/YF all are relative to ET time zone 4:00AM EST. This is usually 9:00 in London, 
        // but in 2020-03-12, when USA set summer time zone 2 weeks early, the 4:00AM cutoff time was 8:00AM in London. And IB/YF measured itself to EST time, not UTC time. Implement this behaviour.
        // >UTC-0:50: (at night).

        // ***************************************************
        // IEX specific only
        // https://github.com/iexg/IEX-API
        // https://cloud.iexapis.com/stable/stock/market/batch?symbols=QQQ&types=quote&token=<...>  takes about 250ms, no matter how many tickers are queried, so it is quite fast.
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
        // https://github.com/iexg/IEX-API/issues/357
        // This is available in /stock/quote https://iextrading.com/developer/docs/#quote
        // extendedPrice, extendedChange, extendedChangePercent, extendedPriceTime
        // These represent prices from 8am-9:30am and 4pm-5pm. We are aiming to cover the full pre/post market hours in a future version.
        // https://github.com/iexg/IEX-API/issues/693
        // >Use /stock/aapl/quote, this will return extended hours data (8AM - 5PM), "on Feb 26, 2019"
        // "I have built a pretty solid scanner and research platform on IEX, but for live trading IEX is obviously not suitable (yet?). I hope one day IEX will provide truly real-time data. Otherwise, I am pretty happy with IEX so far. Been using it for 2 years now/"
        // "We offer true real time IEX trades and quotes. IEX is the only exchange that provides free market data."
        // >"// Paid account: $1 per 1 million messages/mo: 1000000/30/20/60 = 28 messages per minute." But maybe it is infinite. Just every 1M messages is $1. The next 1M messages is another $1. Etc. that is likely. Good. So, we don't have to throttle it, just be careful than only download data if it is needed.
        // --------------------------------------------------------------
        // ------------------ Problems of IEX:
        // - pre/Postmarket only: 8am-9:30am and 4pm-5pm, when Yahoo has it from 9:00 UTC. So, it is not enough.
        // - cut-off time is too late. Until 14:30 asking PreviousDay, it still gives the price 2 days ago. When YF will have premarket data at 9:00. Although "latestPrice" can be used as close.
        // - the only good thing: in market-hours, RT prices are free, and very quick to obtain and batched.


        Timer? m_rtTimer;
        bool m_rtTimerRunning = false;
        object m_rtTimerLock = new Object();

        ManualResetEventSlim m_rtMres = new ManualResetEventSlim(false);     // 50ns (vs. 1000ns of ManualResetEvent). 50x faster. Because no reliance of operation system. http://www.albahari.com/threading/part2.aspx

        static int m_rtTimerFrequencyRegularMs = 3000;    // as a demo go with 3sec, later change it to 5sec, do decrease server load.
        static int m_rtTimerFrequencyPrePostMs = 60000; 

        uint[] m_rtAssetIds = new uint[0];

        DateTime m_lastDownloadLastPrice = DateTime.MinValue;
        uint m_nIexDownload = 0;
        uint m_nYfDownload = 0;
        public void ServerDiagnosticRealtime(StringBuilder p_sb)
        {
            p_sb.Append($"Realtime: m_rtTimerRunning: {m_rtTimerRunning}, m_nYfDownload: {m_nYfDownload}, m_nIexDownload:{m_nIexDownload} <br>");
        }
        public void RtTimer_Elapsed(object? state)    // Timer is coming on a ThreadPool thread
        {
            // Download the RT price immediately, don't sleep, but set up Timer that it is called either 3 sec or 60 sec later. Until there is a 5 minute when nobody accessed RT prices.
            try
            {
                Utils.Logger.Info("RtTimer_Elapsed(). BEGIN");
                 if (!m_rtTimerRunning)
                     return; // if it was disabled by another thread in the meantime, we should not waste resources to execute this.

                var tradingHoursNow = Utils.UsaTradingHoursNow();

                m_lastDownloadLastPrice = DateTime.UtcNow;

                if (tradingHoursNow == TradingHours.RegularTrading)
                {
                    DownloadLastPriceIex(m_rtAssetIds);
                }
                else
                {
                    DownloadLastPriceYF(m_rtAssetIds, tradingHoursNow);
                }

                m_rtMres.Set();
                // m_rtMres.Reset();   // This doesn't work here. If we reset it too quickly, clients doesn't get the signal.

                lock (m_rtTimerLock)
                {
                    if (m_rtTimerRunning)
                    {
                        TimeSpan tsSinceLastRtCall = DateTime.UtcNow - m_lastGetLastRtCall;
                        if (tsSinceLastRtCall <= TimeSpan.FromSeconds(5 * 60))  // only repeat it, if there was a function call in the last 5 minutes 
                            m_rtTimer!.Change(TimeSpan.FromMilliseconds((tradingHoursNow == TradingHours.RegularTrading) ? m_rtTimerFrequencyRegularMs : m_rtTimerFrequencyPrePostMs), TimeSpan.FromMilliseconds(-1.0));
                        else
                            m_rtTimerRunning = false;
                    }
                }
                Utils.Logger.Info("RtTimer_Elapsed(). END");
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "RtTimer_Elapsed() exception.");
                throw;
            }
        }

        
        DateTime m_lastGetLastRtCall = DateTime.MinValue;
        public IEnumerable<(AssetId32Bits SecdID, float LastPrice)> GetLastRtPrice(uint[] p_assetIds)     // C# 7.0 adds tuple types and named tuple literals. uint[] is faster to create and more RAM efficient than linked-list<uint>
        {
            m_lastGetLastRtCall = DateTime.UtcNow;
            lock (m_rtTimerLock)
            {
                if (!m_rtTimerRunning)      // if it is not running, start it immediately
                {
                    Utils.Logger.Info("GetLastRtPrice(). Starting m_rtTimer.");
                    m_rtAssetIds = p_assetIds;      // at the moment, it only bring RT price for the list of securities which was used when GetLastRtPrice() was called first. Later, it will be better.
                    if (m_rtTimer == null)
                        m_rtTimer = new System.Threading.Timer(new TimerCallback(RtTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
                    m_rtTimerRunning = true;
                    m_rtTimer.Change(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(-1.0));     // runs immediately, but only once. To avoid that it runs parallel, if first one doesn't finish
                }
            }

            var tradingHoursNow = Utils.UsaTradingHoursNow();
            TimeSpan tsSinceLastRtDownload = DateTime.UtcNow - m_lastDownloadLastPrice;
            if (tsSinceLastRtDownload > TimeSpan.FromSeconds(80))  // if price is too old, wait for RT background thread to signal successful price.
            {
                m_rtMres.Reset();
                 bool isSignalledNoTimeout = m_rtMres.Wait(TimeSpan.FromSeconds(90));
            }
            
            return p_assetIds.Select(r =>
                {
                    var sec = GetAsset(r);
                    return (sec.AssetId, (tradingHoursNow == TradingHours.RegularTrading) ? sec.LastPriceIex : sec.LastPriceYF);
                });
        }


        void DownloadLastPriceYF(uint[] p_assetIDs, TradingHours p_tradingHoursNow)  // takes ? ms from WinPC
        {
            Utils.Logger.Info("DownloadLastPriceYF() START");
            m_nYfDownload++;
            try
            {
                // https://query1.finance.yahoo.com/v7/finance/quote?symbols=AAPL,AMZN  returns all the fields.
                // https://query1.finance.yahoo.com/v7/finance/quote?symbols=QQQ%2CSPY%2CGLD%2CTLT%2CVXX%2CUNG%2CUSO&fields=symbol%2CregularMarketPreviousClose%2CregularMarketPrice%2CmarketState%2CpostMarketPrice%2CpreMarketPrice  // returns just the specified fields.
                // "marketState":"PRE" or "marketState":"POST", In PreMarket both "preMarketPrice" and "postMarketPrice" are returned.
                var symbols = p_assetIDs.Select(r => GetAsset(r).LastTicker).ToArray();
                var quotes = Yahoo.Symbols(symbols).Fields(new Field[] { Field.Symbol, Field.RegularMarketPreviousClose, Field.RegularMarketPrice, Field.MarketState, Field.PostMarketPrice, Field.PreMarketPrice }).QueryAsync().Result;
                foreach (var quote in quotes)
                {
                    Asset? sec = null;
                    foreach (var secdID in p_assetIDs)
                    {
                        var s = GetAsset(secdID);
                        if (s.LastTicker == quote.Key)
                        {
                            sec = s;
                            break;
                        }
                    }

                    if (sec != null)
                    {
                        dynamic lastPrice = float.NaN;
                        string fieldStr = (p_tradingHoursNow == TradingHours.PreMarket) ? "PreMarketPrice" : "PostMarketPrice";
                        // TLT doesn't have premarket data. https://finance.yahoo.com/quote/TLT  "quoteSourceName":"Delayed Quote", while others: "quoteSourceName":"Nasdaq Real Time Price"
                        if (!quote.Value.Fields.TryGetValue(fieldStr, out lastPrice))
                            lastPrice = (float)quote.Value.RegularMarketPrice;  // fallback: the last regular-market Close price both in Post and next Pre-market

                        sec.LastPriceYF = (float)lastPrice;
                    }
                }
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "DownloadLastPriceYF()");
            }
        }

        void DownloadLastPriceIex(uint[] p_assetIds)  // takes 450-540ms from WinPC
        {
            Utils.Logger.Info("DownloadLastPriceIex() START");
            m_nIexDownload++;
            try
            {
                if (!Request_api_iextrading_com(string.Format("https://api.iextrading.com/1.0/tops?symbols={0}", String.Join(", ", p_assetIds.Select(r => GetAsset(r).LastTicker))), out HttpWebResponse? response) || (response == null))
                    return;

                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), ASCIIEncoding.ASCII))
                {
                    string responseText = reader.ReadToEnd();
                    Utils.Logger.Info("DownloadLastPriceIex() str = '{0}'", responseText);
                    ExtractAttributeIex(responseText, "lastSalePrice", p_assetIds);
                }
                response.Close();

            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "DownloadLastPriceIex()");
            }
        }

        private void ExtractAttributeIex(string responseText, string p_attribute, uint[] p_assetIds)
        {
            int iStr = 0;   // this is the fastest. With IndexOf(). Not using RegEx, which is slow.
            while (iStr < responseText.Length)
            {
                int bSymbol = responseText.IndexOf("symbol\":\"", iStr);
                if (bSymbol == -1)
                    break;
                bSymbol += "symbol\":\"".Length;
                int eSymbol = responseText.IndexOf("\"", bSymbol);
                if (eSymbol == -1)
                    break;
                string ticker = responseText.Substring(bSymbol, eSymbol - bSymbol);
                int bAttribute = responseText.IndexOf(p_attribute + "\":", eSymbol);
                if (bAttribute == -1)
                    break;
                bAttribute += (p_attribute + "\":").Length;
                int eAttribute = responseText.IndexOf(",\"", bAttribute);
                if (eAttribute == -1)
                    break;
                string attributeStr = responseText.Substring(bAttribute, eAttribute - bAttribute);
                // only search ticker among the stocks p_assetIds. Because duplicate tickers are possible in the MemDb.Securities, but not expected in p_assetIds
                Asset? asset = null;
                foreach (var secdID in p_assetIds)
                {
                    var s = GetAsset(secdID);
                    if (s.LastTicker == ticker)
                    {
                        asset = s;
                        break;
                    }
                }

                if (asset != null)
                {
                    float.TryParse(attributeStr, out float attribute);
                    switch (p_attribute)
                    {
                        case "previousClose":
                            // sec.PreviousCloseIex = attribute;
                            break;
                        case "lastSalePrice":
                            asset.LastPriceIex = attribute;
                            break;
                    }

                }
                iStr = eAttribute;
            }
        }

        // compared to IB data stream, IEX is sometimes 5-10 sec late. But sometimes it is not totally accurate. It is like IB updates its price every second. IEX updates randomli. Sometimes it updates every 1 second, sometime after 10seconds. In general this is fine.
        // "We limit requests to 100 per second per IP measured in milliseconds, so no more than 1 request per 10 milliseconds."
        // https://iexcloud.io/pricing/ 
        // Free account: 50,000 core messages/mo, That is 50000/30/20/60 = 1.4 message per minute. 
        // Paid account: $1 per 1 million messages/mo: 1000000/30/20/60 = 28 messages per minute.
        // But maybe it is infinite. Just every 1M messages is $1. The next 1M messages is another $1. Etc. that is likely. Good. So, we don't have to throttle it, just be careful than only download data if it is needed.
        // At the moment 'tops' works without token, as https://api.iextrading.com/1.0/tops?symbols=QQQ,SPY,TLT,GLD,VXX,UNG,USO
        // but 'last' or other PreviousClose calls needs token: https://api.iextrading.com/1.0/lasts?symbols=QQQ,SPY,TLT,GLD,VXX,UNG,USO
        // Solution: query real-time lastPrice ever 2 seconds, but query PreviousClose only once a day.
        // This doesn't require token: https://api.iextrading.com/1.0/tops?symbols=AAPL,GOOGL
        // PreviousClose data requires token: https://cloud.iexapis.com/stable/stock/market/batch?symbols=AAPL,FB&types=quote&token=<get it from sensitive-data file>
        static bool Request_api_iextrading_com(string p_uri, out HttpWebResponse? response)
        {
            response = null;
            try
            {
                //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("https://api.iextrading.com/1.0/stock/market/batch?symbols={0}&types=quote", p_tickerString));
                //HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("https://api.iextrading.com/1.0/last?symbols={0}", p_tickerString));       // WebExceptionStatus.ProtocolError: "Not Found"
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(p_uri);
                request.KeepAlive = true;
                request.Headers.Set(HttpRequestHeader.CacheControl, "max-age=0");
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36";
                request.Headers.Add("Upgrade-Insecure-Requests", @"1");
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Headers.Set(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
                request.Headers.Set(HttpRequestHeader.AcceptLanguage, "hu-HU,hu;q=0.9,en-US;q=0.8,en;q=0.7");
                //request.Headers.Set(HttpRequestHeader.Cookie, @"_ga=GA1.2.889468537.1517554268; ctoken=<...from SqFramework  source...>");    // it is probably an old token. Not useful. Not necessary.
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                Utils.Logger.Error("Request_api_iextrading_com() WebException");
                if (e.Status == WebExceptionStatus.ProtocolError)
                    response = (HttpWebResponse)e.Response;
                else
                    return false;
            }
            catch (Exception)
            {
                Utils.Logger.Error("Request_api_iextrading_com() Exception");
                if (response != null)
                    response.Close();
                return false;
            }
            return true;
        }

    }

}