using System;
using System.Collections.Generic;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Providers price scaling factors for a permanent tick
    /// </summary>
    public interface IFactorProvider : IEnumerable<IFactorRow>
    {
        /// <summary>
        /// Gets the symbol this factor file represents
        /// </summary>
        public string Permtick { get; }

        /// <summary>
        /// The minimum tradeable date for the symbol
        /// </summary>
        /// <remarks>
        /// Some factor files have INF split values, indicating that the stock has so many splits
        /// that prices can't be calculated with correct numerical precision.
        /// To allow backtesting these symbols, we need to move the starting date
        /// forward when reading the data.
        /// Known symbols: GBSN, JUNI, NEWL
        /// </remarks>
        public DateTime? FactorFileMinimumDate { get; set; }
        // SqCore Change NEW:
        public bool HasInfSplitValuesProblem { get; set; } // introduced to avoid unnecessary 'numerical precision issues in the factor file' warnings. In SqCore, we assume we will Not have Inf precision problem stocks.
        // SqCore Change END

        /// <summary>
        /// Gets the price factor for the specified search date
        /// </summary>
        decimal GetPriceFactor(DateTime searchDate, DataNormalizationMode dataNormalizationMode, DataMappingMode? dataMappingMode = null, uint contractOffset = 0);
    }
}
