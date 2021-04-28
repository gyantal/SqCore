
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using SqCommon;

namespace FinTechCommon
{
    // All Assets gSheet: https://docs.google.com/spreadsheets/d/1gkZlvD5epmBV8zi-l0BbLaEtwScvhHvHOYFZ4Ah_MA4/edit#gid=898941432

    [DebuggerDisplay("SqTicker = {SqTicker}, AssetId({AssetId})")]
    public class Asset
    {
        public AssetId32Bits AssetId { get; set; } = AssetId32Bits.Invalid; // Unique assetId for code. Faster than SqTicker. Invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
		public string Symbol { get; set; } = string.Empty;	// can be shown on the UI
		public string Name { get; set; } = string.Empty;
		public string ShortName { get; set; } = string.Empty;
		public CurrencyId Currency { get; set; } = CurrencyId.USD;  // if stocks with different currencies are in the portfolio they have to be converted to USD, if they are not. IB has a BaseCurrency of the account. We use USD as the base currency of the program. Every calculations are based in the USD form.

		public string SqTicker { get; set; } = string.Empty;    // Unique assetId for humans to read. Better to store it as static field then recalculating at every request in derived classes

        private float m_lastValue = float.NaN; // field
        public DateTime LastValueUtc { get; set; } = DateTime.MinValue;
        public float LastValue // real-time last price. Value is better than Price, because NAV, and ^VIX index has value, but it is not a price.
        {
            get { return m_lastValue; }
            set
            {
                m_lastValue = value;
                LastValueUtc = DateTime.UtcNow;
            }
        }
		public Asset()
        {
        }

		public Asset(AssetId32Bits assetId, string symbol, string name, string shortName, CurrencyId currency)
		{
			AssetId = assetId;
            Symbol = symbol;
            Name = name;
            ShortName = shortName;
			Currency = currency;

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

			SqTicker = AssetHelper.gAssetTypeCode[assetType] + "/" + Symbol;	// by default it is good for most Assets. "C/EUR", "D/GBP.USD", "N/DC.IM"
        }

		public static string BasicSqTicker(AssetType assetType, string symbol)
		{
			return AssetHelper.gAssetTypeCode[assetType] + "/" + symbol;
		}

    }

	public class Cash : Asset
    {
        public Cash(JsonElement row) : base(AssetType.CurrencyCash, row)
        {
        }
	}

    public class CurrPair : Asset
    {
		public CurrencyId TargetCurrency { get; set; } = CurrencyId.Unknown;
		public string TradingSymbol { get; set; } = string.Empty;

		public DateTime ExpectedHistoryStartDateLoc { get; set; } = DateTime.MaxValue; // not necessarily ET. Depends on the asset.

		public CurrPair(JsonElement row) : base(AssetType.CurrencyPair, row)
        {
            string symbol = row[1].ToString()!;
            int iDot = symbol.IndexOf('.');
            string targetCurrencyStr = iDot == -1 ? symbol : symbol.Substring(0, iDot); // prepared for symbol "HUF.EUR" or "HUF". But we choose the symbol without the '.'

            TargetCurrency = AssetHelper.gStrToCurrency[targetCurrencyStr];
            TradingSymbol = row[5].ToString()!;

			string baseCurrencyStr = row[4].ToString()!;
            if (String.IsNullOrEmpty(baseCurrencyStr))
                baseCurrencyStr = "USD";
			SqTicker = SqTicker + "." + baseCurrencyStr;	// The default SqTicker just add the "D/HUF", the target currency. But we have to add the base currency as for the pair to be unique.
        }
    }

	public class RealEstate : Asset
    {
		public User? User { get; set; } = null;

		public RealEstate(JsonElement row, User[] users) : base(AssetType.RealEstate, row)
        {
			User = users.FirstOrDefault(r => r.Username == row[5].ToString()!);
        }
	}

	public class BrokerNav : Asset
    {
		public User? User { get; set; } = null;

		public DateTime ExpectedHistoryStartDateLoc { get; set; } = DateTime.MaxValue;	// not necessarily ET. Depends on the asset.

		public List<BrokerNav> AggregateNavChildren { get; set; } = new List<BrokerNav>();

		public BrokerNav(JsonElement row, User[] users) : base(AssetType.BrokerNAV, row)
        {
			User = users.FirstOrDefault(r => r.Username == row[5].ToString()!);
			if (User == null)
				throw new SqException($"BrokerNAV asset '{SqTicker}' should have a user.");
        }

		public BrokerNav(AssetId32Bits assetId, string symbol, string name, string shortName, CurrencyId currency, User user, DateTime histStartDate, List<BrokerNav> aggregateNavChildren)
			: base(assetId, symbol, name, shortName, currency)
        {
			User = user;
			ExpectedHistoryStartDateLoc = histStartDate;
			AggregateNavChildren = aggregateNavChildren;
        }
		
        public bool IsAggregatedNav
        {
            get { return (AggregateNavChildren.Count  > 0); }   // N/GA.IM, N/DC.IM, N/DC.ID, N/DC , AggregatedNav has no '.'.
        }
    }

	public class Portfolio : Asset
    {
		public User? User { get; set; } = null;

		public Portfolio(JsonElement row, User[] users) : base(AssetType.Portfolio, row)
        {
			User = users.FirstOrDefault(r => r.Username == row[5].ToString()!);
        }
	}

	public class Company : Asset
    {
		public string ExpirationDate { get; set; } = string.Empty;	// maybe convert it to DateTime in the future

		public string SymbolHist { get; set; } = string.Empty;
		public string NameHist { get; set; } = string.Empty;

		public string GicsSector { get; set; } = string.Empty;
		public string GicsSubIndustry { get; set; } = string.Empty;
		public Company(JsonElement row) : base(AssetType.Company, row)
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

		public string ExpirationDate { get; set; } = string.Empty;	// maybe convert it to DateTime in the future

		public string SymbolHist { get; set; } = string.Empty;
		public string NameHist { get; set; } = string.Empty;

		public string YfTicker { get; set; } = string.Empty;
		public string Flags { get; set; } = string.Empty;
		public string ISIN { get; set; } = string.Empty; // International Securities Identification Number would be a unique identifier. Not used for now.

		public Asset? Company { get; set; } = null;	// if not StockType.ETF

		public DateTime ExpectedHistoryStartDateLoc { get; set; } = DateTime.MaxValue; // not necessarily ET. Depends on the asset.

		public Stock(JsonElement row, List<Asset> assets) : base(AssetType.Stock, row)
        {
			StockType = AssetHelper.gStrToStockType[row[5].ToString()!];
			PrimaryExchange = row[6].ToString()!;
			TradingSymbol = string.IsNullOrEmpty(row[7].ToString()) ? Symbol : row[7].ToString()!; // if not given, use Symbol by default
			ExpirationDate = row[8].ToString()!;
			SymbolHist = row[9].ToString()!;
			NameHist = row[10].ToString()!;
			YfTicker = string.IsNullOrEmpty(row[11].ToString()) ? Symbol : row[11].ToString()!; // if not given, use Symbol by default
			Flags = row[12].ToString()!;
			ISIN = row[13].ToString()!;

            if (StockType != StockType.ETF)
            {
                string companySqTicker = row[14].ToString()!;
				if (String.IsNullOrEmpty(companySqTicker))	// if not specified, assume the Stock.Symbol
					companySqTicker = Symbol;
                string seekedSqTicker = AssetHelper.gAssetTypeCode[AssetType.Company] + "/" + companySqTicker;
                var comps = assets.FindAll(r => r.AssetId.AssetTypeID == AssetType.Company && r.SqTicker == seekedSqTicker);
                if (comps == null)
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
        {    // Maybe it is not necessary to store in DB. If a VXX becomes dead, we can change LastTicker = "VXX-20190130", so actually IsAlive can be computed
            get { return ExpirationDate == string.Empty; }   // '-' should not be in the last ticker
        }
    }
}