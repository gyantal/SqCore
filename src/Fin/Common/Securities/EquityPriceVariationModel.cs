﻿using System;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides an implementation of <see cref="IPriceVariationModel"/>
    /// for use in defining the minimum price variation for a given equity
    /// under Regulation NMS – Rule 612 (a.k.a – the “sub-penny rule”)
    /// </summary>
    public class EquityPriceVariationModel : SecurityPriceVariationModel
    {
        /// <summary>
        /// Get the minimum price variation from a security
        /// </summary>
        /// <param name="parameters">An object containing the method parameters</param>
        /// <returns>Decimal minimum price variation of a given security</returns>
        public override decimal GetMinimumPriceVariation(GetMinimumPriceVariationParameters parameters)
        {
            if (parameters.Security.Type != SecurityType.Equity)
            {
                throw new ArgumentException(Invariant($"EquityPriceVariationModel.GetMinimumPriceVariation(): Invalid SecurityType: {parameters.Security.Type}"));
            }

            // If the quotation is priced less than $1.00 per share, the minimum pricing increment is $0.0001.
            // Source: https://www.law.cornell.edu/cfr/text/17/242.612
            if (parameters.ReferencePrice < 1m)
            {
                return 0.0001m;
            }

            return base.GetMinimumPriceVariation(parameters);
        }
    }
}