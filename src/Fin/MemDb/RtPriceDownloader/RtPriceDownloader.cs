using System.Text;
using System.Threading.Tasks;
using SqCommon;

namespace Fin.MemDb;

// ***************************************************
// YF, IB, IEX preMarket, postMarket behaviour
// >UTC-7:50: YF:  no pre-market price. IB: there is no ask-bid, but there is an indicative value somehow, because Dashboard bar shows QQQ: 216.13 (+0.25%). I checked, that is the 'Mark price'. IB estimates that (probably for pre-market margin calls). YF and others will never have that, because it is sophisticated.
// >UTC-8:50: YF:  no pre-market price. IB: there is no ask-bid, but previously good indicative value went back to PreviousClose, so ChgPct = 0%, so in PreMarket that far away from Open, this MarkPrice is not very useful. IB did reset the price, because preparing for pre-market open in 10min.
// >UTC-9:10: YF (started at 9:00, there is premarket price: "Pre-Market: 4:03AM EST"), IB: There is Ask-bid spread. This is 5.5h before market open. That should be enough.
// So, I don't need IB indicative MarkPrice. IB AccInfo website is also good, showing QQQ change. IEX: IEX shows some false data, which is not yesterday close,
// but probably last day postMarket lastPrice sometime, which is not useful.
// So, in pre-market this IEX 'top' cannot be used. Investigated. Even IEX cloud can be used in pre-market. ("lastSalePrice":0)
// It is important here, because of summer/winter time zones that IB/YF all are relative to ET time zone 4:00AM EST. This is usually 9:00 in London,
// but in 2020-03-12, when USA set summer time zone 2 weeks early, the 4:00AM cutoff time was 8:00AM in London. And IB/YF measured itself to EST time, not UTC time. Implement this behaviour.
// >As a last resort, if free batch (wheny many symbols are queried) price queries fails (YF's quote and Iex Top), we can try individual downloads freely for some small number of assets.
// We have 1000 assets, but only about 80 is really important. The ones traded with UberVxx/UberTaa with webtools, and the MarketDashboard BrokerAccViewer.
// This Last resort individual (not batch) RT downloads can be used for VOD.L, on which Iex fails. E.g. https://query1.finance.yahoo.com/v8/finance/chart/VOD.L works, individually.
public class RtPriceDownloader // can use different downloaders (Yf, Iex, Ib) based on which has free data
{
    readonly RtPriceDownloaderYf m_rtPriceDownloaderYf = new();
    readonly RtPriceDownloaderIex m_rtPriceDownloaderIex = new();
    readonly RtPriceDownloaderIb m_rtPriceDownloaderIb = new();
    readonly bool m_useYfForLastPrice = true;
    readonly bool m_useYfForPriorClose = true;

    // Intraday, for frequent queries getting LastPrice without PriorClose is enough. E.g. for IEX TOPS can query 1000 symbols, but only for RT prices. IEX MARKET queries PriorClose too, but only for 100 symbols
    internal async Task DownloadLastPrice(Asset[] p_assets)
    {
        if (m_useYfForLastPrice)
            await m_rtPriceDownloaderYf.DownloadLastPrice(p_assets);
        else
            await m_rtPriceDownloaderIex.DownloadLastPrice(p_assets);
    }

    // Asset.PriorClose is pushed from historical prices for the historical assets only (about #100).
    // For the other 90% of assets, there is no historical data. But PriorClose is needed for many tools. So, get these also once per day from the RtDownloaders
    internal async Task DownloadPriorCloseAndLastPrice(Asset[] p_assets)
    {
        if (m_useYfForPriorClose)
            await m_rtPriceDownloaderYf.DownloadPriorCloseAndLastPrice(p_assets);
        else
            await m_rtPriceDownloaderIex.DownloadPriorCloseAndLastPrice(p_assets);
    }

    internal void DownloadLastPriceOptions(Asset[] p_options)
    {
        m_rtPriceDownloaderIb.DownloadLastPriceOptions(p_options);
    }

    internal void RtTimerUpdate(RtFreqParam p_freqParam, Asset[] p_assets)
    {
        // IEX is faster (I guess) and we don't risk that YF bans our server for crucial historical data. So, don't query YF too frequently.
        // But we prefer YF, because IEX returns "lastSalePrice":0, while YF returns RT correctly for these 6 stocks: BIB,IDX,MVV,RTH,VXZ,LBTYB
        // https://cloud.iexapis.com/stable/tops?token=<...>&symbols=BIB,IDX,MVV,RTH,VXZ,LBTYB
        // https://query1.finance.yahoo.com/v7/finance/quote?symbols=BIB,IDX,MVV,RTH,VXZ,LBTYB
        // Therefore, use IEX only for High/Mid Freq, and only in RegularTrading.
        // LowFreq is the all 900 tickers. For that we need those 6 assets as well.

        if (p_freqParam.RtFreq == RtFreq.HighFreq) // 2023-06-12: disable HighFreq (RTH: every 30sec), because Iex 50K free monthly quote already expired in 12 days. Probably switch it back next month.
            return;

        // bool useIexRt = p_freqParam.RtFreq != RtFreq.LowFreq && Utils.UsaTradingHoursExNow_withoutHolidays() == TradingHoursEx.RegularTrading; // use IEX only for High/Mid Freq, and only in RegularTrading.
        bool useIexRt = false; // 2023-06-12: disable Iex, because Iex 50K free monthly quote already expired in 12 days. Probably switch it back next month.

        if (p_freqParam.RtFreq == RtFreq.LowFreq)
            m_rtPriceDownloaderYf.DownloadPriorCloseAndLastPrice(p_assets).TurnAsyncToSyncTask();

        if (useIexRt)
            m_rtPriceDownloaderIex.DownloadLastPrice(p_assets).TurnAsyncToSyncTask();
        else
            m_rtPriceDownloaderYf.DownloadLastPrice(p_assets).TurnAsyncToSyncTask();
    }

    public void ServerDiagnostic(StringBuilder p_sb)
    {
        p_sb.Append($"Realtime: actual non-empty m_nYfDownload: {m_rtPriceDownloaderYf.NumDownload}, actual non-empty m_nIexDownload:{m_rtPriceDownloaderIex.NumDownload}. ");
    }
}