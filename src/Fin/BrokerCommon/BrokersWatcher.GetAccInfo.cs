using System;
using System.Collections.Generic;
using System.Linq;
using IBApi;

namespace Fin.BrokerCommon;

public class AccInfo
{
    public string BrAccStr { get; set; } = string.Empty;
    public Gateway Gateway { get; set; }
    public List<BrAccSum> AccSums = new();   // AccSummary
    public List<BrAccPos> AccPoss = new();   // Positions

    public AccInfo(string brAccStr, Gateway gateway)
    {
        BrAccStr = brAccStr;
        Gateway = gateway;
    }
}

public class BrAccSum
{
    public string Tag { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}

public static class BrAccSumHelper
{
    public static double GetValue(this List<BrAccSum> accSums, string tagStr)
    {
        string valStr = accSums.First(r => r.Tag == tagStr).Value;
        if (!double.TryParse(valStr, out double valDouble))
            valDouble = Double.NegativeInfinity; // Math.Round() would crash on NaN
        return (int)Math.Round(valDouble, MidpointRounding.AwayFromZero); // 0.5 is rounded to 1, -0.5 is rounded to -1. Good.
    }
}

public class BrAccPos
{
    public string SqTicker { get; set; } = string.Empty;
    public uint AssetId { get; set; } = 0;  // AssetId.Invalid = 0;  we cannot store Asset pointers, because FinTechCommon is a higher module than BrokerCommon. Although we can store Objects that point to Assets
    public object? AssetObj { get; set; } = null;
    public Contract Contract { get; set; }
    public int FakeContractID { get; set; } // when we cannot change Contract.ConID which should be left 0, but we use an Int in the dictionary.
    public double Position { get; set; } // in theory, position is Int (whole number) for all the examples I seen. However, IB gives back as double, just in case of a complex contract. Be prepared.
    public double AvgCost { get; set; }
    public double EstPrice { get; set; } = Double.NaN;  // MktValue can be calculated
    public double EstUndPrice { get; set; } // In case of options DeliveryValue can be calculated

    public bool IsHidingFromClient { get; set; } = false;
    public int MktDataID { get; set; } = -1;    // for reqMktData
    public double AskPrice { get; set; } = Double.NaN;
    public double BidPrice { get; set; } = Double.NaN;
    public double LastPrice { get; set; } = Double.NaN;
    public double IbMarkPrice { get; set; } = Double.NaN;       // streamed (non-snapshot) mode. Usually LastPrice, but if Last is not in Ask-Bid range, then Ask or Bid, whichever makes sense

    public KeyValuePair<int, List<BrAccPos>> UnderlyingDictItem { get; set; }

    public double IbComputedImpVol { get; set; } = Double.NaN;
    public double IbComputedDelta { get; set; } = Double.NaN;
    public double IbComputedUndPrice { get; set; } = Double.NaN;

    public BrAccPos(Contract contract)
    {
        Contract = contract;
    }
}

public partial class BrokersWatcher
{
    public List<BrAccSum>? GetAccountSums(GatewayId p_gatewayId)
    {
        var gateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_gatewayId);
        if (gateway == null)
            return null;
        return gateway.GetAccountSums();
    }

    public List<BrAccPos>? GetAccountPoss(GatewayId p_gatewayId)
    {
        var gateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_gatewayId);
        if (gateway == null || !gateway.IsConnected)
            return null;

        return gateway.GetAccountPoss(Array.Empty<string>());
    }
}