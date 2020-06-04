
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace FinTechCommon
{
    [DebuggerDisplay("LastTicker = {LastTicker}, AssetId({AssetId})")]
    public class Asset
    {
        public AssetId32Bits AssetId { get; set; } = AssetId32Bits.Invalid; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64

        public String ISIN { get; set; } = String.Empty;    // International Securities Identification Number would be a unique identifier. Not used for now.
        public ExchangeId PrimaryExchange { get; set; } = ExchangeId.Unknown; // different assed with the same "VOD" ticker can exist in LSE, NYSE; YF uses "VOD" and "VOD.L"
        public string LastTicker { get; set; } = String.Empty;  // a security has a LastTicker Now, but in the past it might have a different ticker before ticker rename
        public List<TickerChange> TickerChanges { get; set; } = new List<TickerChange>();
        public string ExpectedHistorySpan { get; set; } = String.Empty;
        public float LastPriceIex { get; set; } = -100.0f;     // real-time last price
        public float LastPriceYF { get; set; } = -100.0f;     // real-time last price
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
}