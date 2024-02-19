using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Fin.Base;
using Fin.BrokerCommon;
using SqCommon;

namespace Fin.MemDb;

// All Assets gSheet: https://docs.google.com/spreadsheets/d/1gkZlvD5epmBV8zi-l0BbLaEtwScvhHvHOYFZ4Ah_MA4/edit#gid=1251256843

[DebuggerDisplay("SqTicker = {SqTicker}, AssetId({AssetId})")]
public class Asset
{
    public AssetId32Bits AssetId { get; set; } = AssetId32Bits.Invalid; // Unique assetId for code. Faster than SqTicker. Invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64

    // Symbol can be shown on the UI. It is the IB Symbol, so it is "BRK B", not "BRK-B", which is YfTicker or "BRK.B", which is IexTicker.
    // For option "O/SVXY*220121P15", the Symbol is only "SVXY". This symbol is shown in BrAccViewer table, so it should be only 3-4 chars.
    public string Symbol { get; set; } = string.Empty;
    public string SymbolEx { get; set; } = string.Empty;    // EXtended Symbol for options, futures. E.g. "SVXY 220121P15", show on UI. Need this, 'coz Asset.Symbol = "SVXY", kept short. Asset.Name is long company name. Asset.ShortName is only 1-2 char for currencies.
    public string Name { get; set; } = string.Empty;    // longer company name or ETF name. For VXX, it is "iPath Series B S&P 500 VIX Short-Term Futures ETN"
    public string ShortName { get; set; } = string.Empty;   // only used for currencies, for very short 1-2 chars
    public CurrencyId Currency { get; set; } = CurrencyId.USD;  // if stocks with different currencies are in the portfolio they have to be converted to USD, if they are not. IB has a BaseCurrency of the account. We use USD as the base currency of the program. Every calculations are based in the USD form.

    public string SqTicker { get; set; } = string.Empty;    // Unique assetId for humans to read. Better to store it as static field then recalculating at every request in derived classes

    public bool IsDbPersisted { get; set; } = true;   // There are special adHoc temporary, runtime-only Stocks, Options coming from IB-TWS. They don't exist in the RedisDb. They cannot be part of a Buy transaction in a Portfolio.

    public DateTime ExpectedHistoryStartDateLoc { get; set; } = DateTime.MaxValue; // Local date, not necessarily ET. Depends on the asset. Too many assets have valueHistory: properties. Stock obviusly have price history. But even public Companies can be valued historically based on MarketCap.

    // The Last (realtime price) and the PriorClose values are too frequently used (for daily %Chg calculation) in many Asset classes: Stocks, Futures, Options. Although it is not necessary in other classes, but let them have here in the parent class
    // Don't name it LastPrice. Because it might refer to LastTrade Price.
    // EstPrice can be calculated from Ask/Bid, even if there is no Last Trade price (as Options may not trade even 1 contracts for days, so there is no Last Trade, but we estimate the price from RT Ask/Bid)
    // EstValue is a similar concept to IB's MarkPrice. An estimated price (Mark-to-Mark) that is used for margin calculations. It has a discretionary calculation, and can based on Ask/Bid/LastTrade (if happened recently)
    private float m_estValue = float.NaN; // field
    public DateTime EstValueTimeUtc { get; set; } = DateTime.MinValue;

    public DateTime EstValueTimeLoc
    {
        get
        {
            if (EstValueTimeUtc == DateTime.MinValue)
                return DateTime.MinValue;
            // future work: this can be implemented more sophisticated. Now, we assume all assets are USA assets, but if asset is traded in EU countries, we have to convert to EU timezone.
            return Utils.ConvertTimeFromUtcToEt(EstValueTimeUtc);
        }
    }
    public float EstValue // real-time last price. Value is better than Price, because NAV, and ^VIX index has value, but it is not a price.
    {
        get
        {
            return m_estValue;
        }

        set
        {
            m_estValue = value;
            EstValueTimeUtc = DateTime.UtcNow;
        }
    }

    public float PriorClose { get; set; } = float.NaN;  // IB calls it PriorClose, YF, Iex calls PreviousClose. Nobody calls it "Last"Close, because that is better to use for the "Last"-price

    public static string BasicSqTicker(AssetType assetType, string symbol)
    {
        return AssetHelper.gAssetTypeCode[assetType] + "/" + symbol;
    }

    public Asset()
    {
    }

    public Asset(AssetId32Bits assetId, string symbol, string name, string shortName, CurrencyId currency, bool isDbPersisted = true)
    {
        AssetId = assetId;
        Symbol = symbol;
        Name = name;
        ShortName = shortName;
        Currency = currency;
        IsDbPersisted = isDbPersisted;

        SqTicker = AssetHelper.gAssetTypeCode[AssetId.AssetTypeID] + "/" + Symbol;
    }

    public Asset(AssetType assetType, JsonElement row)
    {
        AssetId = new AssetId32Bits(assetType, uint.Parse(row[0].ToString()!));
        Symbol = row[1].ToString()!;
        Name = row[2].ToString()!;
        ShortName = row[3].ToString()!;

        string baseCurrencyStr = row[4].ToString()!;
        if (String.IsNullOrEmpty(baseCurrencyStr))
            baseCurrencyStr = "USD";
        Currency = AssetHelper.gStrToCurrency[baseCurrencyStr];

        SqTicker = AssetHelper.gAssetTypeCode[assetType] + "/" + Symbol;    // by default it is good for most Assets. "C/EUR", "D/GBP.USD", "N/DC.IM"
    }

    public virtual IBApi.Contract? MakeIbContract()
    {
        return null;
    }
}

public class Cash : Asset
{
    public Cash(JsonElement row)
        : base(AssetType.CurrencyCash, row)
    {
    }
}

public class CurrPair : Asset
{
    public CurrencyId TargetCurrency { get; set; } = CurrencyId.Unknown;
    public string TradingSymbol { get; set; } = string.Empty;

    public CurrPair(JsonElement row)
        : base(AssetType.CurrencyPair, row)
    {
        string symbol = row[1].ToString()!;
        int iDot = symbol.IndexOf('.');
        string targetCurrencyStr = iDot == -1 ? symbol : symbol[..iDot]; // prepared for symbol "HUF.EUR" or "HUF". But we choose the symbol without the '.'

        TargetCurrency = AssetHelper.gStrToCurrency[targetCurrencyStr];
        TradingSymbol = row[5].ToString()!;

        string baseCurrencyStr = row[4].ToString()!;
        if (String.IsNullOrEmpty(baseCurrencyStr))
            baseCurrencyStr = "USD";
        SqTicker = SqTicker + "." + baseCurrencyStr;    // The default SqTicker just add the "D/HUF", the target currency. But we have to add the base currency as for the pair to be unique.
    }
}

public class FinIndex : Asset // C# 8.0 has System.Index and System.Range for the new Array notation. Instead of Index, FinIndex is a better name: FinancialIndex
{
    public string YfTicker { get; set; } = string.Empty;
    public FinIndex(JsonElement row)
        : base(AssetType.FinIndex, row)
    {
        YfTicker = "^" + Symbol; // use Symbol by default
    }
}

public class RealEstate : Asset
{
    public User? User { get; set; } = null;

    public RealEstate(JsonElement row, User[] users)
        : base(AssetType.RealEstate, row)
    {
        User = users.FirstOrDefault(r => r.Username == row[5].ToString()!);
    }
}

public class Company : Asset
{
    public string ExpirationDate { get; set; } = string.Empty;  // maybe convert it to DateTime in the future

    public string SymbolHist { get; set; } = string.Empty;
    public string NameHist { get; set; } = string.Empty;

    public string GicsSector { get; set; } = string.Empty;
    public string GicsSubIndustry { get; set; } = string.Empty;
    public Company(JsonElement row)
        : base(AssetType.Company, row)
    {
        ExpirationDate = row[5].ToString()!;
        SymbolHist = row[6].ToString()!;
        NameHist = row[7].ToString()!;
        GicsSector = row[8].ToString()!;
        GicsSubIndustry = row[9].ToString()!;

        if (!string.IsNullOrEmpty(ExpirationDate))
            SqTicker = SqTicker + "*" + ExpirationDate;
    }
}

public class Stock : Asset
{
    public StockType StockType { get; set; } = StockType.Unknown;
    public string PrimaryExchange { get; set; } = string.Empty;
    public ExchangeId PrimaryExchangeId { get; set; } = ExchangeId.Unknown; // different assed with the same "VOD" ticker can exist in LSE, NYSE; YF uses "VOD" and "VOD.L"

    public string TradingSymbol { get; set; } = string.Empty;

    public string ExpirationDate { get; set; } = string.Empty;  // maybe convert it to DateTime in the future

    public string SymbolHist { get; set; } = string.Empty;
    public string NameHist { get; set; } = string.Empty;

    public string YfTicker { get; set; } = string.Empty;

    // IbSymbol: "BRK B" YFTicker: "BRK-B", but IEX gets it only as "BRK.B"
    // https://cloud.iexapis.com/stable/tops?token=<...>&symbols=BRK B     => returns empty string
    // https://cloud.iexapis.com/stable/tops?token=<...>&symbols=BRK-B     => returns empty string
    // https://cloud.iexapis.com/stable/tops?token=<...>&symbols=BRK.B     => returns correctly
    public string IexTicker { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string ISIN { get; set; } = string.Empty; // International Securities Identification Number would be a unique identifier. Not used for now.

    public Asset? Company { get; set; } = null; // if not StockType.ETF
    public Stock(JsonElement row, List<Asset> assets)
        : base(AssetType.Stock, row)
    {
        StockType = AssetHelper.gStrToStockType[row[5].ToString()!];
        PrimaryExchange = row[6].ToString()!;
        TradingSymbol = string.IsNullOrEmpty(row[7].ToString()) ? Symbol : row[7].ToString()!; // if not given, use Symbol by default
        ExpirationDate = row[8].ToString()!;
        SymbolHist = row[9].ToString()!;
        NameHist = row[10].ToString()!;
        YfTicker = string.IsNullOrEmpty(row[11].ToString()) ? Symbol : row[11].ToString()!; // if not given, use Symbol by default
        IexTicker = YfTicker.Replace('-', '.'); // change "BRK-B" to "BRK.B"
        Flags = row[12].ToString()!;
        ISIN = row[13].ToString()!;

        if (StockType != StockType.ETF)
        {
            string companySqTicker = row[14].ToString()!;
            if (String.IsNullOrEmpty(companySqTicker)) // if not specified, assume the Stock.Symbol
                companySqTicker = Symbol;
            string seekedSqTicker = AssetHelper.gAssetTypeCode[AssetType.Company] + "/" + companySqTicker;
            var comps = assets.FindAll(r => r.AssetId.AssetTypeID == AssetType.Company && r.SqTicker == seekedSqTicker);
            if (comps == null || comps.Count == 0)
                throw new SqException($"Company SqTicker '{seekedSqTicker}' was not found.");
            if (comps.Count > 1)
                throw new SqException($"To many ({comps.Count}) Company SqTicker '{seekedSqTicker}' was found.");
            Company = comps[0];
        }
        if (!string.IsNullOrEmpty(PrimaryExchange))
            SqTicker = SqTicker + "^" + PrimaryExchange[0];
        if (!string.IsNullOrEmpty(ExpirationDate))
            SqTicker = SqTicker + "*" + ExpirationDate;
    }

    public bool IsAlive
    { // Maybe it is not necessary to store in DB. If a VXX becomes dead, we can change LastTicker = "VXX-20190130", so actually IsAlive can be computed
        get { return ExpirationDate == string.Empty; } // '-' should not be in the last ticker
    }

    public override IBApi.Contract? MakeIbContract()
    {
        return VBrokerUtils.MakeStockContract(Symbol);
    }
}