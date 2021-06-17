using System;
using System.Collections.Generic;
using BrokerCommon;

namespace FinTechCommon
{
    public class BrPortfolio
    {
        public GatewayId GatewayId { get; set; } = GatewayId.Unknown;
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;

        public double NetLiquidation { get; set; } = double.NaN;    // NAV is updated realtime, but at LastUpdate timestamp these are the valid values of GrossPosValue, etc.
        public double GrossPositionValue { get; set; } = double.NaN;
        public double TotalCashValue { get; set; } = double.NaN;
        public double InitMarginReq { get; set; } = double.NaN;
        public double MaintMarginReq { get; set; } = double.NaN;
        public List<AccPos> AccPoss { get; set; } = new List<AccPos>();

    }

}