using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqCommon;
using YahooFinanceApi;

namespace Fin.MemDb;

// 2023-05-25: YF: "API-level access to Yahoo Finance quotes data has been disabled." We have to assume, YF RT will never work again. Reason. It is expensive. Pay the licenses. https://stackoverflow.com/questions/76059562/yahoo-finance-api-get-quotes-returns-invalid-cookie

public class RtPriceDownloaderYf
{
    uint m_nDownload = 0;

    public uint NumDownload { get => m_nDownload; }

    public async Task DownloadLastPrice(Asset[] p_assets)
    {
        await Download(p_assets, false);
    }

    public async Task DownloadPriorCloseAndLastPrice(Asset[] p_assets)
    {
        await Download(p_assets, true);
    }

    private async Task Download(Asset[] p_assets, bool p_updatePriorClose)
    {
        Utils.Logger.Debug("RtPriceDownloaderYf.Download() START");
        m_nDownload++;
        try
        {
            var tradingHoursNow = Utils.UsaTradingHoursExNow_withoutHolidays();
            string lastValFieldStr = tradingHoursNow switch
            {
                TradingHoursEx.PrePreMarketTrading => "PostMarketPrice",    // YF data fields ([R]egularMarketPrice) have to be capitalized in C# even though the JSON data has JS notation, starting with lowercase.
                TradingHoursEx.PreMarketTrading => "PreMarketPrice",
                TradingHoursEx.RegularTrading => "RegularMarketPrice",
                TradingHoursEx.PostMarketTrading => "PostMarketPrice",
                TradingHoursEx.Closed => "PostMarketPrice",
                _ => throw new SqException($"Not expected p_tradingHoursNow value: {tradingHoursNow}"),
            };

            // What field to excract for PriorClose from YF?
            // > At the weekend, we would like to see the Friday data, so regularMarketPreviousClose (Thursday) is fine.
            // >PrePreMarket: What about 6:00GMT on Monday? That is 1:00ET. That is not regular trading yet, which starts at 4:00ET. But IB shows Friday closes at that time is PriorClose. We would like to see Friday closes as well. So, in PrePreMarket, use regularMarketPrice
            // >If we are in PreMarket trading (then proper RT prices will come), then use regularMarketPrice
            // >If we are RTH or PostMarket, or Close, use regularMarketPreviousClose. That way, at the weekend, we can observe BrAccViewer table as it was at Friday night.
            string priorCloseFieldStr = tradingHoursNow switch
            {
                TradingHoursEx.PrePreMarketTrading => "RegularMarketPrice",
                TradingHoursEx.PreMarketTrading => "RegularMarketPrice",
                TradingHoursEx.RegularTrading => "RegularMarketPreviousClose",
                TradingHoursEx.PostMarketTrading => "RegularMarketPreviousClose",
                TradingHoursEx.Closed => "RegularMarketPreviousClose",
                _ => throw new SqException($"Not expected p_tradingHoursNow value: {tradingHoursNow}"),
            };

            // https://query1.finance.yahoo.com/v7/finance/quote?symbols=AAPL,AMZN  returns all the fields.
            // https://query1.finance.yahoo.com/v7/finance/quote?symbols=QQQ%2CSPY%2CGLD%2CTLT%2CVXX%2CUNG%2CUSO&fields=symbol%2CregularMarketPreviousClose%2CregularMarketPrice%2CmarketState%2CpostMarketPrice%2CpreMarketPrice  // returns just the specified fields.
            // "marketState":"PRE" or "marketState":"POST", In PreMarket both "preMarketPrice" and "postMarketPrice" are returned.
            string[] yfTickers = p_assets.Select(r =>
            {
                if (r is Stock stock)
                    return stock.YfTicker;
                else if (r is FinIndex finIndex)
                    return finIndex.YfTicker;
                else
                    throw new SqException($"YfTicker doesn't exist for asset {r.SqTicker}");
            }).ToArray();
            Dictionary<string, bool> yfTickersReceived = yfTickers.ToDictionary(r => r, r => false);
            IReadOnlyDictionary<string, Security> quotes = await Yahoo.Symbols(yfTickers).Fields(new Field[] { Field.Symbol, Field.RegularMarketPreviousClose, Field.RegularMarketPrice, Field.MarketState, Field.PostMarketPrice, Field.PreMarketPrice, Field.PreMarketChange }).QueryAsync();  // takes 45 ms from WinPC (30 tickers)

            int nReceivedAndRecognized = 0;
            foreach (var quote in quotes)
            {
                string yfTicker = quote.Key;
                Asset? sec = null;
                foreach (var a in p_assets)
                {
                    if (a is Stock stock && stock.YfTicker == yfTicker)
                    {
                        sec = a;
                        break;
                    }
                    else if (a is FinIndex finIndex && finIndex.YfTicker == yfTicker)
                    {
                        sec = a;
                        break;
                    }
                }

                if (sec != null)
                {
                    nReceivedAndRecognized++;
                    yfTickersReceived[yfTicker] = true;
                    // TLT doesn't have premarket data. https://finance.yahoo.com/quote/TLT  "quoteSourceName":"Delayed Quote", while others: "quoteSourceName":"Nasdaq Real Time Price"
                    dynamic? lastVal = float.NaN;
                    if (!quote.Value.Fields.TryGetValue(lastValFieldStr, out lastVal))
                        lastVal = (float)quote.Value.RegularMarketPrice;  // fallback: the last regular-market Close price both in Post and next Pre-market
                    sec.EstValue = (float)lastVal;

                    // If there was a VXX split today morning, YF doesn't show it for a while during the day and priorCloseFieldStr is non-adjusted. Bad.
                    // in that case "regularMarketPrice" = "regularMarketPreviousClose" are the old values.
                    // the only way to get it from the data YF gives is that there is a "preMarketChange":0.32999802, which is a $value. We can substract it from the "preMarketPrice" to calculate previous close
                    // for many ETFs (thinly traded), there is no PreMarketPrice or PreMarketChange
                    // Another option would be to not use this YF.PriorClose at all (which is buggy), but use the historical prices (last value), because that properly has the split adjustment for yesterday close
                    if (p_updatePriorClose)
                    {
                        dynamic? preMarketChange = null;
                        bool isSplitSurePriorCloseCalculation = tradingHoursNow == TradingHoursEx.PreMarketTrading && quote.Value.Fields.TryGetValue("PreMarketChange", out preMarketChange);
                        if (isSplitSurePriorCloseCalculation) // TODO: we might check here that we only do this IF there was a split event today morning.
                        {
                            sec.PriorClose = sec.EstValue - (float)preMarketChange!;
                        }
                        else
                        {
                            if (quote.Value.Fields.TryGetValue(priorCloseFieldStr, out dynamic? priorClose))
                                sec.PriorClose = (float)priorClose;
                        }
                    }

                    // if (sec.SqTicker == "I/VIX")
                    //     Utils.Logger.Info($"VIX priorClose: {sec.PriorClose}, lastVal:{sec.EstValue}");  // TEMP
                }
            }

            // if (p_assets.Length > 100) // only called in LowFreq timer.
            //     Utils.Logger.Info($"DownloadPriorCloseAndLastPriceYF: #queried:{yfTickers.Length}, #received:{nReceivedAndRecognized}");  // TEMP

            if (nReceivedAndRecognized != yfTickers.Length)
            {
                string msg = $"RtPriceDownloaderYf.Download() problem. #queried:{yfTickers.Length}, #received:{nReceivedAndRecognized}. Missing yfTickers: {String.Join(",", yfTickersReceived.Where(r => !r.Value).Select(r => r.Key))}";
                Console.WriteLine(msg);
                Utils.Logger.Warn(msg);
            }
        }
        catch (Exception e)
        {
            Utils.Logger.Error(e, "RtPriceDownloaderYf.Download() crash");
        }
    }
}