using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Data
{
    /// <summary>
    /// Provides convenient methods for holding several <see cref="SubscriptionDataConfig"/>
    /// </summary>
    public class SubscriptionDataConfigList : List<SubscriptionDataConfig>
    {
        /// <summary>
        /// <see cref="Symbol"/> for which this class holds <see cref="SubscriptionDataConfig"/>
        /// </summary>
        public Symbol Symbol { get; private set; }

        /// <summary>
        /// Assume that the InternalDataFeed is the same for both <see cref="SubscriptionDataConfig"/>
        /// </summary>
        public bool IsInternalFeed
        {
            get
            {
                var first = this.FirstOrDefault();
                return first != null && first.IsInternalFeed;
            }
        }

        /// <summary>
        /// Default constructor that specifies the <see cref="Symbol"/> that the <see cref="SubscriptionDataConfig"/> represent
        /// </summary>
        /// <param name="symbol"></param>
        public SubscriptionDataConfigList(Symbol symbol)
        {
            Symbol = symbol;
        }

        /// <summary>
        /// Sets the <see cref="DataNormalizationMode"/> for all <see cref="SubscriptionDataConfig"/> contained in the list
        /// </summary>
        /// <param name="normalizationMode"></param>
        public void SetDataNormalizationMode(DataNormalizationMode normalizationMode)
        {
            if (Symbol.SecurityType.IsOption() && normalizationMode != DataNormalizationMode.Raw)
            {
                throw new ArgumentException($"DataNormalizationMode.Raw must be used with SecurityType {Symbol.SecurityType}");
            }

            foreach (var config in this)
            {
                config.DataNormalizationMode = normalizationMode;
            }
        }
    }
}
