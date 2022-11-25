using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Fin.BrokerCommon;
using SqCommon;

namespace Fin.MemDb;

public class BrokerNav : Asset
{
    public User? User { get; set; } = null;

    public BrokerNav? AggregateNavParent { get; set; } = null;
    public List<BrokerNav> AggregateNavChildren { get; set; } = new List<BrokerNav>();

    public GatewayId GatewayId { get; set; } = GatewayId.Unknown;

    public BrokerNav(JsonElement row, User[] users)
        : base(AssetType.BrokerNAV, row)
    {
        User = users.FirstOrDefault(r => r.Username == row[5].ToString()!);
        if (User == null)
            throw new SqException($"BrokerNAV asset '{SqTicker}' should have a user.");

        if (GatewayExtensions.NavSymbol2GatewayId.TryGetValue(Symbol, out GatewayId gatewayId))
            GatewayId = gatewayId;
    }

    public BrokerNav(AssetId32Bits assetId, string symbol, string name, string shortName, CurrencyId currency, bool isDbPersisted, User user, DateTime histStartDate, List<BrokerNav> aggregateNavChildren)
        : base(assetId, symbol, name, shortName, currency, isDbPersisted)
    {
        User = user;
        ExpectedHistoryStartDateLoc = histStartDate;
        AggregateNavChildren = aggregateNavChildren;

        if (GatewayExtensions.NavSymbol2GatewayId.TryGetValue(Symbol, out GatewayId gatewayId))
            GatewayId = gatewayId;
    }

    public bool IsAggregatedNav
    {
        get { return AggregateNavChildren.Count > 0; } // N/GA.IM, N/DC.IM, N/DC.ID, N/DC , AggregatedNav has no '.'.
    }
}