using System;
using System.Collections.Generic;
using Fin.Base;

namespace Fin.MemDb;

public enum CountryId : byte // there are 192 countries in the world. warning: 2009-06: the Company.BaseCountryID is in reality CountryCode
{
    UnitedStates = 1,
    UnitedKingdom = 2,
    China = 3,
    Japan = 4,
    Germany = 5,
    France = 6,
    Canada = 7,
    Russia = 8,
    Brazil = 9,
    India = 10,
    Hungary = 11,

    // DBUtils.g_defaultMarketHolidays exploits that 0 < CountryID.USA,UK,Germany < 20

    Unknown = 255
}

public enum StockIndexId : short // According to dbo.StockIndex
{
    SP500 = 1,
    VIX,
    Nasdaq,
    DowJones,
    Russell2000,
    Russell1000,
    PHLX_Semiconductor,
    VXN,
    Unknown = -1
}

public class Split
{
    public DateTime Date { get; set; }

    public double Before { get; set; }
    public double After { get; set; }
}

public enum StockType : byte
{
    Unknown = 0,
    Common = 1,
    ADR = 2,
    ETF = 3,
    Virtual = 4 // VXX.SQ is a virtual stock
}

public enum OptionType : byte
{
    Unknown = 0,
    StockOption = 1,
    IndexOption = 2 // in IB for VIX options, the underlying is "VIX index", because "VIX index" is also the underlying of VIX futures. This is better. Don't introduce a chain that "VIX options" => "VIX futures" => "VIX index"
}

public enum OptionRight : byte // Put or Call: right to buy or sell
{
    Unknown = 0,
    Call = 1,
    Put = 2
}

public enum SharedAccess : byte
{
    Unknown = 0,
    Restricted = 1, // the default: means owner and admins can see it.
    OwnerOnly = 2,  // hidden even from admin users.
    Anyone = 3, // totally public. All users can see that in their 'Shared with Anyone' folder.
}

public enum PortfolioType : byte
{
    Unknown = 0,
    Trades = 1, // trades come from the SqCore RedisDB
    Simulation = 2,
    LegacyDbTrades = 3, // trades come from the SqDesktop SqlDB
}

// Not used. Copied from HqFramework for getting ideas; if we need an in-memory data structure for TickerHistory
// public interface ITickerProvider
// {
//     /// <summary> Returns those inputs that does NOT belong to this provider
//     /// and prepares the answer for those that do belong to it.
//     /// See ITickerProvider.Prepare&lt;&gt;() extension for more. </summary>
//     IEnumerable<AssetId32Bits> Prepare(IEnumerable<AssetId32Bits> p_assets);
//     /// <summary> Returns null if does not know the answer </summary>
//     string GetTicker(AssetType p_at, int p_subTableId, DateTime p_timeUtc);
//     /// <summary> Returns 0 (=default(AssetId32Bits)) if does not know the answer, or p_ticker is empty </summary>
//     AssetId32Bits ParseTicker(string p_ticker, DateTime p_timeUtc, AssetType p_requested = AssetType.Unknown);
// }

public static class AssetHelper
{
    // https://stackoverflow.com/questions/16100/convert-a-string-to-an-enum-in-c-sharp
    // performance of Enum.Parse() is awful, because it is implemented via reflection.
    // I've measured 3ms to convert a string to an Enum on the first run, on a desktop computer. (Just to illustrate the level of awfullness).
    public static readonly Dictionary<string, CurrencyId> gStrToCurrency = new()
    {
        { "NaN", CurrencyId.Unknown },
        { "USD", CurrencyId.USD },
        { "EUR", CurrencyId.EUR },
        { "GBP", CurrencyId.GBP },
        { "GBX", CurrencyId.GBX },
        { "HUF", CurrencyId.HUF },
        { "JPY", CurrencyId.JPY },
        { "CNY", CurrencyId.CNY },
        { "CAD", CurrencyId.CAD },
        { "CHF", CurrencyId.CHF }
    };

    public static readonly Dictionary<CurrencyId, string> gCurrencyToString = new()
    {
        { CurrencyId.Unknown, "NaN" },
        { CurrencyId.USD, "USD" },
        { CurrencyId.EUR, "EUR" },
        { CurrencyId.GBP, "GBP" },
        { CurrencyId.GBX, "GBX" },
        { CurrencyId.HUF, "HUF" },
        { CurrencyId.JPY, "JPY" },
        { CurrencyId.CNY, "CNY" },
        { CurrencyId.CAD, "CAD" },
        { CurrencyId.CHF, "CHF" }
    };

    public static readonly Dictionary<string, StockType> gStrToStockType = new()
    {
        { "NaN", StockType.Unknown },
        { string.Empty, StockType.Common },
        { "ADR", StockType.ADR },
        { "ETF", StockType.ETF },
        { "VIR", StockType.Virtual }
    };

    public static readonly Dictionary<AssetType, char> gAssetTypeCode = new()
    {
        { AssetType.Unknown, '?' },
        { AssetType.CurrencyCash, 'C' },
        { AssetType.CurrencyPair, 'D' },    // P is for Portfolio, so I don't want P as Pair. Just use D, that is the next letter after C in the ABC.
        { AssetType.Stock, 'S' },   // default is Stock
        // { AssetType.StockVirtual, 'V' },   // Virtual stocks like VXX.SQ with ExpirationDate, better to be a normal 'S' stock with Stock.Type=Virtual
        { AssetType.Bond, 'B' },
        { AssetType.Fund, 'U' },
        { AssetType.Futures, 'F' },
        { AssetType.Option, 'O' },
        { AssetType.Commodity, 'M' },
        { AssetType.RealEstate, 'R' },
        { AssetType.FinIndex, 'I' },
        { AssetType.BrokerNAV, 'N' },
        { AssetType.Portfolio, 'P' },   // Portfolio is like a virtual separated BrokerNAV. One BrokerNAV can contain many smaller portfolios.
        { AssetType.GeneralTimeSeries, 'T' },
        { AssetType.Company, 'A' },
    };

    public static readonly Dictionary<char, AssetType> gChrToAssetType = new()
    {
        { '?', AssetType.Unknown },
        { 'C', AssetType.CurrencyCash },
        { 'D', AssetType.CurrencyPair },
        { 'S', AssetType.Stock },
        { 'B', AssetType.Bond },
        { 'U', AssetType.Fund },
        { 'F', AssetType.Futures },
        { 'O', AssetType.Option },
        { 'M', AssetType.Commodity },
        { 'R', AssetType.RealEstate },
        { 'I', AssetType.FinIndex },
        { 'N', AssetType.BrokerNAV },
        { 'P', AssetType.Portfolio },
        { 'T', AssetType.GeneralTimeSeries },
        { 'A', AssetType.Company },
    };

    public static readonly Dictionary<string, SharedAccess> gStrToSharedAccess = new()
    {
        { string.Empty, SharedAccess.Restricted },
        { "Restricted", SharedAccess.Restricted },
        { "OwnerOnly", SharedAccess.OwnerOnly },
        { "Anyone", SharedAccess.Anyone }
    };

    public static readonly Dictionary<string, PortfolioType> gStrToPortfolioType = new()
    {
        { string.Empty, PortfolioType.Trades },
        { "Trades", PortfolioType.Trades },
        { "Simulation", PortfolioType.Simulation },
        { "LegacyDbTrades", PortfolioType.LegacyDbTrades }
    };
    public static readonly Dictionary<TradeAction, string> gTradeActionToStr = new()
    {
        { TradeAction.Unknown, string.Empty },
        { TradeAction.Deposit, "DPT" },
        { TradeAction.Withdrawal, "WTD" },
        { TradeAction.Buy, "BOT" },
        { TradeAction.Sell, "SLD" },
        { TradeAction.Exercise, "EXC" },
        { TradeAction.Expired, "EXP" }
    };
    public static readonly Dictionary<string, TradeAction> gStrToTradeAction = new()
    {
        { string.Empty, TradeAction.Unknown },
        { "DPT", TradeAction.Deposit },
        { "WTD", TradeAction.Withdrawal },
        { "BOT", TradeAction.Buy },
        { "SLD", TradeAction.Sell },
        { "EXC", TradeAction.Exercise },
        { "EXP", TradeAction.Expired }
    };
    public static readonly Dictionary<ExchangeId, string> gExchangeToStr = new()
    {
        { ExchangeId.Unknown, string.Empty },
        { ExchangeId.NASDAQ, "NASDAQ" },
        { ExchangeId.NYSE, "NYSE" },
        { ExchangeId.AMEX, "AMEX" },
        { ExchangeId.PINK, "PINK" },
        { ExchangeId.CDNX, "CDNX" },
        { ExchangeId.LSE, "LSE" },
        { ExchangeId.XTRA, "XTRA" },
        { ExchangeId.CBOE, "CBOE" },
        { ExchangeId.ARCA, "ARCA" },
        { ExchangeId.BATS, "BATS" },
        { ExchangeId.OTCBB, "OTCBB" }
    };
    public static readonly Dictionary<string, ExchangeId> gStrToExchange = new()
    {
        { string.Empty, ExchangeId.Unknown },
        { "NASDAQ", ExchangeId.NASDAQ },
        { "NYSE", ExchangeId.NYSE },
        { "AMEX", ExchangeId.AMEX },
        { "PINK", ExchangeId.PINK },
        { "CDNX", ExchangeId.CDNX },
        { "LSE", ExchangeId.LSE },
        { "XTRA", ExchangeId.XTRA },
        { "CBOE", ExchangeId.CBOE },
        { "ARCA", ExchangeId.ARCA },
        { "BATS", ExchangeId.BATS },
        { "OTCBB", ExchangeId.OTCBB }
    };
    // This can find both "VOD" (Vodafone) ticker in LSE (in GBP), NYSE (in USD).
    public static List<Stock> GetAllMatchingStocksBySymbol(this List<Asset> p_assets, string p_symbol, ExchangeId p_primExchangeID = ExchangeId.Unknown, DateTime? p_timeUtc = null)
    {
        if (p_timeUtc != null)
            throw new NotImplementedException();    // use Asset.TickerChanges list for historical queries.

        List<Stock> result = new();
        foreach (var sec in p_assets)
        {
            if (sec is not Stock stock)
                continue;
            bool accepted = false;
            if (p_primExchangeID == ExchangeId.Unknown) // if exchange is not known, assume all exchange is good
            {
                if (stock.Symbol == p_symbol)
                {
                    accepted = true;
                }
            }
            else
            {
                if (stock.Symbol == p_symbol && stock.PrimaryExchangeId == p_primExchangeID)
                {
                    accepted = true;
                }
            }

            if (accepted)
                result.Add(stock);
        }
        return result;
    }

    public static Stock? GetFirstMatchingStockBySymbol(this List<Asset> p_assets, string p_symbol, ExchangeId p_primExchangeID = ExchangeId.Unknown)
    {
        foreach (var sec in p_assets)
        {
            if (sec is not Stock stock)
                continue;
            if (p_primExchangeID == ExchangeId.Unknown) // if exchange is not known, assume all exchange is good
            {
                if (stock.Symbol == p_symbol)
                {
                    return stock;
                }
            }
            else
            {
                if (stock.Symbol == p_symbol && stock.PrimaryExchangeId == p_primExchangeID)
                {
                    return stock;
                }
            }
        }
        return null;
    }
}