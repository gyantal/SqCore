using System;
using System.Collections.Generic;
using BrokerCommon;

namespace FinTechCommon
{
     // intially named: BrPortfolio. Was not a good terminology, because we have an other Portfolio as Asset. That Portfolio has historical price. This BrokerPortfolio is just a snapshot.
     // a better name: BrokerAccount, although we also have a Virtual Aggregated BrokerAccount, but still better.
     // Broker Accounts have features like MarginRequirement. A Portfolio is just a list of assets. It shouldn't care about Margin.
    public class BrAccount
    {
        public GatewayId GatewayId { get; set; } = GatewayId.Unknown;
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;

        public double NetLiquidation { get; set; } = double.NaN;    // NAV is updated realtime, but at LastUpdate timestamp these are the valid values of GrossPosValue, etc.
        public double GrossPositionValue { get; set; } = double.NaN;
        public double TotalCashValue { get; set; } = double.NaN;
        public double InitMarginReq { get; set; } = double.NaN;
        public double MaintMarginReq { get; set; } = double.NaN;
        public List<BrAccPos> AccPoss { get; set; } = new List<BrAccPos>();

    }

}