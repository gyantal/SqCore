using System;
using System.Collections.Generic;
using System.Text.Json;
using Fin.BrokerCommon;
using SqCommon;

namespace Fin.MemDb;

public class Option : Asset
{
    public OptionType OptionType { get; set; } = OptionType.Unknown;
    public string UnderlyingSymbol { get; set; } = string.Empty;    // Stock or "VIX index", because "VIX index" is also the underlying of VIX futures.
    public Asset? UnderlyingAsset { get; set; } = null;
    public string LastTradeDateOrContractMonthStr { get; set; } = string.Empty;     // commodity (oil, natgas) futures options may have not a precise date here, but only a ContractMonth
    public DateTime ExpirationDate { get; set; } = SqDateOnly.NO_DATE;    // for VIX options, this is 1 day after the LastTradeDate
    public DateTime LastTradeDate { get; set; } = SqDateOnly.NO_DATE;     // this is given by ID in LastTradeDateOrContractMonthStr. IB can contain the time in it as well: "DEC 21'21 15:15 CST"
    public OptionRight OptionRight { get; set; } = OptionRight.Unknown;     // Put or Call
    public double Strike { get; set; } = double.NaN;
    public int Multiplier { get; set; } = -1;

    public string PrimaryExchange { get; set; } = string.Empty;
    public ExchangeId PrimaryExchangeId { get; set; } = ExchangeId.Unknown; // different assed with the same "VOD" ticker can exist in LSE, NYSE; YF uses "VOD" and "VOD.L"

    public double IbCompDelta { get; set; } = double.NaN;   // needed for BrAccViewer

    public string IbLocalSymbol { get; set; } = string.Empty;   // used only for Debug purposes.
    public IBApi.Contract? IbContract { get; set; } = null;     // used only for Debug purposes.

    public Option(AssetId32Bits assetId, string symbol, string name, string shortName, CurrencyId currency, bool isDbPersisted,
        OptionType optionType, string optionSymbol, string underlyingSymbol, string lastTradeDateOrContractMonth, OptionRight optionRight, double strike,
        int multiplier, string ibLocalSymbol, IBApi.Contract ibContract)
        : base(assetId, symbol, name, shortName, currency, isDbPersisted)
    {
        // an IB example
        // IB-Symbol [string]:"SVXY"
        // IB-LastTradeDateOrContractMonth [string]:"20220121"
        // IB-Right [string]:"P"
        // IB-Strike [double]:15
        // IB-LocalSymbol: "SVXY  220121P00015000"
        // SqTicker: "O/SVXY*220121P15"
        // SqSymbol (for UI): "SVXY 220121P15"
        OptionType = optionType;
        SymbolEx = optionSymbol;
        UnderlyingSymbol = underlyingSymbol;
        LastTradeDateOrContractMonthStr = lastTradeDateOrContractMonth;
        OptionRight = optionRight;
        Strike = strike;
        Multiplier = multiplier;
        IbLocalSymbol = ibLocalSymbol;
        IbContract = ibContract;

        SqTicker = GenerateSqTicker(underlyingSymbol, lastTradeDateOrContractMonth, (optionRight == OptionRight.Call) ? 'C' : ((optionRight == OptionRight.Put) ? 'P' : '?'), strike);
    }

    public static string GenerateOptionSymbol(string p_underlyingSymbol, string p_lastTradeDateOrContractMonth, OptionRight p_optionRight, double p_strike)
    {
        char right = (p_optionRight == OptionRight.Call) ? 'C' : ((p_optionRight == OptionRight.Put) ? 'P' : '?');
        return $"{p_underlyingSymbol} {p_lastTradeDateOrContractMonth}{right}{p_strike}";
    }
    public static string GenerateOptionName(string p_underlyingSymbol, string p_lastTradeDateOrContractMonth, OptionRight p_optionRight, double p_strike)
    {
        string rightStr = (p_optionRight == OptionRight.Call) ? "Call" : ((p_optionRight == OptionRight.Put) ? "Put" : "?");
        return $"{p_underlyingSymbol} {rightStr} option. Exp:{p_lastTradeDateOrContractMonth},  Strike:{p_strike}";
    }

    // generate "O/ARKK*20230120C77.96", "O/VXX*220617P16"
    public static string GenerateSqTicker(string p_underlyingSymbol, string p_lastTradeDateOrContractMonth, char p_right, double p_strike)
    {
        return $"O/{p_underlyingSymbol}*{p_lastTradeDateOrContractMonth}{p_right}{p_strike}";
    }

    public Option(JsonElement row, List<Asset> asset)
        : base(AssetType.Option, row)
    {
        _ = asset; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
        // var stocks = assets.FindAll(r => r.AssetId.AssetTypeID == AssetType.Stock && r.SqTicker == seekedSqTicker);
    }

    public override IBApi.Contract? MakeIbContract()
    {
        // return IbContract;    // Maybe don't use,  coz IbContract contains the ContractID that might be IbGateway specific
        var rightStr = (OptionRight == OptionRight.Call) ? "C" : ((OptionRight == OptionRight.Put) ? "P" : "?");
        return VBrokerUtils.MakeOptionContract(Symbol, rightStr, Strike, Multiplier, LastTradeDateOrContractMonthStr, IbLocalSymbol);
    }
}