﻿using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Util;

namespace QuantConnect.Securities.Volatility
{
    /// <summary>
    /// Represents a base model that computes the volatility of a security
    /// </summary>
    public class BaseVolatilityModel : IVolatilityModel
    {
        /// <summary>
        /// Provides access to registered <see cref="SubscriptionDataConfig"/>
        /// </summary>
        protected ISubscriptionDataConfigProvider SubscriptionDataConfigProvider;

        /// <summary>
        /// Gets the volatility of the security as a percentage
        /// </summary>
        public virtual decimal Volatility { get; }

        /// <summary>
        /// Sets the <see cref="ISubscriptionDataConfigProvider"/> instance to use.
        /// </summary>
        /// <param name="subscriptionDataConfigProvider">Provides access to registered <see cref="SubscriptionDataConfig"/></param>
        public virtual void SetSubscriptionDataConfigProvider(
            ISubscriptionDataConfigProvider subscriptionDataConfigProvider)
        {
            SubscriptionDataConfigProvider = subscriptionDataConfigProvider;
        }

        /// <summary>
        /// Updates this model using the new price information in
        /// the specified security instance
        /// </summary>
        /// <param name="security">The security to calculate volatility for</param>
        /// <param name="data">The new data used to update the model</param>
        public virtual void Update(Security security, BaseData data)
        {
        }

        /// <summary>
        /// Returns history requirements for the volatility model expressed in the form of history request
        /// </summary>
        /// <param name="security">The security of the request</param>
        /// <param name="utcTime">The date/time of the request</param>
        /// <returns>History request object list, or empty if no requirements</returns>
        public virtual IEnumerable<HistoryRequest> GetHistoryRequirements(
            Security security,
            DateTime utcTime
            )
        {
            return Enumerable.Empty<HistoryRequest>();
        }

        /// <summary>
        /// Gets history requests required for warming up the greeks with the provided resolution
        /// </summary>
        /// <param name="security">Security to get history for</param>
        /// <param name="utcTime">UTC time of the request (end time)</param>
        /// <param name="resolution">Resolution of the security</param>
        /// <param name="barCount">Number of bars to lookback for the start date</param>
        /// <returns>Enumerable of history requests</returns>
        /// <exception cref="InvalidOperationException">The <see cref="SubscriptionDataConfigProvider"/> has not been set</exception>
        public IEnumerable<HistoryRequest> GetHistoryRequirements(
            Security security, 
            DateTime utcTime,
            Resolution? resolution,
            int barCount)
        {
            if (SubscriptionDataConfigProvider == null)
            {
                throw new InvalidOperationException(
                    "BaseVolatilityModel.GetHistoryRequirements(): " +
                    "SubscriptionDataConfigProvider was not set."
                );
            }

            var configurations = SubscriptionDataConfigProvider
                .GetSubscriptionDataConfigs(security.Symbol)
                .OrderBy(c => c.TickType)
                .ToList();
            var configuration = configurations.First();
            
            var bar = configuration.Type.GetBaseDataInstance();
            bar.Symbol = security.Symbol;
            
            var historyResolution = resolution ?? bar.SupportedResolutions().Max();

            var periodSpan = historyResolution.ToTimeSpan();
            
            // hour resolution does no have extended market hours data
            var extendedMarketHours = periodSpan != Time.OneHour && configurations.IsExtendedMarketHours();
            var localStartTime = Time.GetStartTimeForTradeBars(
                security.Exchange.Hours,
                utcTime.ConvertFromUtc(security.Exchange.TimeZone),
                periodSpan,
                barCount,
                extendedMarketHours,
                configuration.DataTimeZone);
            var utcStartTime = localStartTime.ConvertToUtc(security.Exchange.TimeZone);

            return new[]
            {
                new HistoryRequest(utcStartTime,
                                   utcTime,
                                   configuration.Type,
                                   configuration.Symbol,
                                   historyResolution,
                                   security.Exchange.Hours,
                                   configuration.DataTimeZone,
                                   historyResolution,
                                   extendedMarketHours,
                                   configurations.IsCustomData(),
                                   configurations.DataNormalizationMode(),
                                   LeanData.GetCommonTickTypeForCommonDataTypes(configuration.Type, security.Type))
            };
        }
    }
}
