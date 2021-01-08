
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using SqCommon;

namespace FinTechCommon
{
    [DebuggerDisplay("LastTicker = {LastTicker}, AssetId({AssetId})")]
    public class Asset
    {
        public AssetId32Bits AssetId { get; set; } = AssetId32Bits.Invalid; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
        public string LastTicker { get; set; } = String.Empty;  // a security has a LastTicker Now, but in the past it might have a different ticker before ticker rename
        public List<TickerChange> TickerChanges { get; set; } = new List<TickerChange>();
		public bool IsAlive {	 // Maybe it is not necessary to store in DB. If a VXX becomes dead, we can change LastTicker = "VXX-20190130", so actually IsAlive can be computed
			get { return LastTicker.IndexOf('-') == -1; }	// '-' should not be in the last ticker
		}	

		public string LastName { get; set; } = String.Empty;
		public List<string> NameChanges { get; set; } = new List<string>();

		// Stock specific fields:
		public CurrencyId Currency { get; set; } = CurrencyId.USD;	// if stocks with different currencies are in the portfolio they have to be converted to USD, if they are not. IB has a BaseCurrency of the account. We use USD as the base currency of the program. Every calculations are based in the USD form.
   		public String ISIN { get; set; } = String.Empty;    // International Securities Identification Number would be a unique identifier. Not used for now.
        public ExchangeId PrimaryExchange { get; set; } = ExchangeId.Unknown; // different assed with the same "VOD" ticker can exist in LSE, NYSE; YF uses "VOD" and "VOD.L"
        public string ExpectedHistorySpan { get; set; } = String.Empty;		// comes from RedisDb
		public DateTime ExpectedHistoryStartDateET { get; set; } = DateTime.MaxValue;   // process ExpectedHistorySpan after Assets Reload, so we don't have to do it 3x per day at historical price reload
        private float m_lastValue = float.NaN; // field
        public float LastValue // real-time last price. Value is better than Price, because NAV, and ^VIX index has value, but it is not a price.
        {
            get { return m_lastValue; }
            set
            {
                m_lastValue = value;
                LastValueUtc = DateTime.UtcNow;
            }
        }
        public DateTime LastValueUtc { get; set; } = DateTime.MinValue;

		public User? User { get; set; } = null;		// *.NAV assets have user_id data

		public bool IsAggregatedNav {
			get { return AssetId.AssetTypeID == AssetType.BrokerNAV && (LastTicker.Count('.') < 2); }	// GA.IM.NAV, DC.IM.NAV, DC.ID.NAV, DC.NAV , AggregatedNav has only one '.'.
		}
    }

	public class AssetInDb	// for quick JSON deserialization. In DB the fields has short names, and not all Asset fields are in the DB anyway
    {
		public AssetType Type { get; set; } = AssetType.Unknown;	// AssetType
        public uint ID { get; set; } = 0; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
        public string Ticker { get; set; } = String.Empty;  // a security has a LastTicker Now, but in the past it might have a different ticker before ticker rename
		public string TickerHist { get; set; } = String.Empty;
		public string Name { get; set; } = String.Empty;
		public string NameHist { get; set; } = String.Empty;
		public string PrimExchg { get; set; } = String.Empty;
		public string user_id { get; set; } = String.Empty;		// *.NAV assets have user_id data
    }

	public class SqCoreWebAssetInDb	// for quick JSON deserialization. In DB the fields has short names, and not all Asset fields are in the DB anyway
    {
        public string AssetId { get; set; } = String.Empty;
		public string LoadPrHist { get; set; } = String.Empty;
    }

    public class TickerChange {
        public DateTime TimeUtc { get; set; } = DateTime.MinValue;
        public String Ticker { get; set; } = String.Empty;
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

	public enum CurrencyId : byte   // there are 192 countries in the world, and less than 192 currencies
	{                               // PortfolioEvaluator.BulkPreparer.Plan() exploits that all values are in 1..62
		USD = 1,
		EUR = 2,
		GBX = 3,
		JPY = 4,
		HUF = 5,
		CNY = 6,
		CAD = 7,
        CHF = 8,
        ILS = 9,
		Unknown = 255
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

	class Split
    {
        public DateTime Date { get; set; }

		public double Before { get; set; }
		public double After { get; set; }
    }
}