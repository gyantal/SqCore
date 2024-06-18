using System;

namespace Fin.Base;

// IB uses ContractType, not AssetType, because it handles contracts that produces assets. E.g. 1 CurrencyPair Forex contract can change 2 assets: both UsdAsset and EurAsset.
// However, we prefer to store Assets in our DB. Irrespective of which contract produced that asset.
// But in general our AssetType and IB's ContractType are interchangeable
// It is possible many different contracts lead to the same Asset.
// E.g. all 3 'BRK B' IbContracts (USA, Swiss, EUR) leads to the same AssetID, same ISIN of the same issuer country.
// Although the currency is different, therefore prices are different. But if I buy any of those contracts, I will get the same Asset.
public enum AssetType : byte
{
    Unknown = 0,     // 0
    CurrencyCash = 1,   // only an asset. Not a contract.
    CurrencyPair = 2,    // Only a contract. Not an asset. IBContractType:Forex;   Not a real asset, because it cannot be owned, but historical daily assetQuotes are needed for conversion
    Stock = 3,      // can be considered as an asset (to own) or a contract to Buy/Sell. Contracts changes two assets: BUY QQQ will add a Stock asset, but takes away an USD cash asset.
    // StockVirtual,          // better to be a normal 'S' stock with Stock.Type=Virtual. Calculated not real traded stocks, such as "VXX.SQ" that needs a real stock VXX pair to complete its history till today
    Bond = 4,
    Fund = 5,           // real fund, not ETF. ETFs are stocks
    Futures = 6,
    Option = 7,
    Commodity = 8,
    RealEstate = 9,
    FinIndex = 10, // SPX, VIX, FTSE
    BrokerNAV = 11,
    Portfolio = 12,      // for Metaportfolios that contain SubPortfolios as assets. AllAsset can contain 2 RealEstate + 2 ISA portfolios + 1 IB BrokerNAV, + 1 Bank (broker) account, and can aggregate all-wealth.
    GeneralTimeSeries = 13,
    Company = 14,   // Fake asset, so SqTicker can use 'A' for companySqTicker unique identification
}

// CashAsset.SubTableID as defined in the BaseAssets tabpage should match this enum
public enum CurrencyId : byte // there are 192 countries in the world, and less than 192 currencies
{ // PortfolioEvaluator.BulkPreparer.Plan() exploits that all values are in 1..62
    Unknown = 0,
    USD = 1,
    EUR = 2,
    GBP = 3,
    GBX = 4,
    HUF = 5,
    JPY = 6,
    CNY = 7,
    CAD = 8,
    CHF = 9
    // Some routines use ~GBX == 252 to indicate GBP, e.g. DBUtils.ConvertToUsd(),ConvertFromUsd(),YQCrawler.CurrencyConverter etc.
}

public enum ExchangeId : sbyte // differs from dbo.StockExchange, which is 'int'
{
    NASDAQ = 1,
    NYSE = 2,       // can be default if USA stock (so currency is USD), otherwise for non-USA stocks it is safer is default is 'Unknown'
    AMEX = 3,
    PINK = 4,       // Pink OTC Markets
    CDNX = 5,       // Canadian Venture Exchange, postfix: ".V"
    LSE = 6,        // London Stock Exchange, postfix: ".L"
    XTRA = 7,      // XETRA, Exchange Electronic Trading (Germany)
    CBOE = 8,
    ARCA = 9,       // NYSE ARCA
    BATS = 10,
    OTCBB = 11,     // OTC Bulletin Boards

    Unknown = -1 // BooleanFilterWith1CacheEntryPerAssetID.CacheRec.StockExchangeID exploits that values fit in an sbyte
                 // TickerProvider.OldStockTickers exploits that values fit in a byte
}

public struct DateValue
{
    public DateTime Date;
    public float Value;
}