using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace FinTechCommon
{

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
		Index = 10, // SPX, VIX
        BrokerNAV = 11,
        Portfolio = 12,      // for Metaportfolios that contain SubPortfolios as assets. AllAsset can contain 2 RealEstate + 2 ISA portfolios + 1 IB BrokerNAV, + 1 Bank (broker) account, and can aggregate all-wealth.
        GeneralTimeSeries = 13,
        Company = 14,   // Fake asset, so SqTicker can use 'A' for companySqTicker unique identification
	}

// CashAsset.SubTableID as defined in the BaseAssets tabpage should match this enum
    public enum CurrencyId : byte   // there are 192 countries in the world, and less than 192 currencies
    {                               // PortfolioEvaluator.BulkPreparer.Plan() exploits that all values are in 1..62
        Unknown = 0,
        USD = 1,
        EUR = 2,
        GBP = 3,
        GBX = 4,
        HUF = 5,
        JPY = 6,
        CNY = 8,
        CAD = 8,
        CHF = 9,
        ILS = 10
        // Some routines use ~GBX == 252 to indicate GBP, e.g. DBUtils.ConvertToUsd(),ConvertFromUsd(),YQCrawler.CurrencyConverter etc.
    }

    
	public enum CountryId : byte    // there are 192 countries in the world. warning: 2009-06: the Company.BaseCountryID is in reality CountryCode
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

	
	public enum StockIndexId : short    // According to dbo.StockIndex
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
		Virtual = 4  // VXX.SQ is a virtual stock
	}

    public enum OptionType : byte
    {
		Unknown = 0,
		StockOption = 1,
		IndexOption = 2 // in IB for VIX options, the underlying is "VIX index", because "VIX index" is also the underlying of VIX futures. This is better. Don't introduce a chain that "VIX options" => "VIX futures" => "VIX index"
	}

    public enum OptionRight : byte  // Put or Call: right to buy or sell
    {
		Unknown = 0,
		Call = 1,
		Put = 2
	}

	public enum ExchangeId : sbyte // differs from dbo.StockExchange, which is 'int'
	{
		NASDAQ = 1,
		NYSE = 2,
		[Description("NYSE MKT LLC")]
		AMEX = 3,
		[Description("Pink OTC Markets")]
		PINK = 4,
		CDNX = 5,       // Canadian Venture Exchange, postfix: ".V"
		LSE = 6,        // London Stock Exchange, postfix: ".L"
		[Description("XTRA")]
		XETRA = 7,      // Exchange Electronic Trading (Germany)
		CBOE = 8,
		[Description("NYSE ARCA")]
		ARCA = 9,
		BATS = 10,
		[Description("OTC Bulletin Boards")]
		OTCBB = 11,

		Unknown = -1    // BooleanFilterWith1CacheEntryPerAssetID.CacheRec.StockExchangeID exploits that values fit in an sbyte
		                // TickerProvider.OldStockTickers exploits that values fit in a byte
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
        public static Dictionary<string, CurrencyId> gStrToCurrency = new Dictionary<string, CurrencyId>() {
            { "NaN", CurrencyId.Unknown},
            { "USD", CurrencyId.USD},
            { "EUR", CurrencyId.EUR},
            { "GBP", CurrencyId.GBP},
            { "GBX", CurrencyId.GBX},
            { "HUF", CurrencyId.HUF},
            { "JPY", CurrencyId.JPY},
            { "CNY", CurrencyId.CNY},
            { "CAD", CurrencyId.CAD},
            { "CHF", CurrencyId.CHF}};

        public static Dictionary<string, StockType> gStrToStockType = new Dictionary<string, StockType>() {
            { "NaN", StockType.Unknown},
            { "", StockType.Common},
            { "ADR", StockType.ADR},
            { "ETF", StockType.ETF},
            { "VIR", StockType.Virtual}};

        public static Dictionary<AssetType, char> gAssetTypeCode = new Dictionary<AssetType, char>() {
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
            { AssetType.Index, 'I' },
            { AssetType.BrokerNAV, 'N' },
            { AssetType.Portfolio, 'P' },   // Portfolio is like a virtual separated BrokerNAV. One BrokerNAV can contain many smaller portfolios.
            { AssetType.GeneralTimeSeries, 'T' },
            { AssetType.Company, 'A' },
             };


        // This can find both "VOD" (Vodafone) ticker in LSE (in GBP), NYSE (in USD).
        public static List<Stock> GetAllMatchingStocksBySymbol(this List<Asset> p_assets, string p_symbol, ExchangeId p_primExchangeID = ExchangeId.Unknown, DateTime? p_timeUtc = null)
        {
            if (p_timeUtc != null)
                throw new NotImplementedException();    // use Asset.TickerChanges list for historical queries.

            List<Stock> result = new List<Stock>();
            foreach (var sec in p_assets)
            {
                Stock? stock = sec as Stock;
                if (stock == null)
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
                Stock? stock = sec as Stock;
                if (stock == null)
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

}