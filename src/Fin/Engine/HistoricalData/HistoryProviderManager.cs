using NodaTime;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;
using QuantConnect.Logging;
using QuantConnect.Util;
using SqCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using HistoryRequest = QuantConnect.Data.HistoryRequest;

namespace QuantConnect.Lean.Engine.HistoricalData
{
    /// <summary>
    /// Provides an implementation of <see cref="IHistoryProvider"/> which
    /// acts as a wrapper to use multiple history providers together
    /// </summary>
    public class HistoryProviderManager : HistoryProviderBase
    {
        private IBrokerage _brokerage;
        private bool _initialized;

        /// <summary>
        /// Collection of history providers being used
        /// </summary>
        /// <remarks>Protected for testing purposes</remarks>
        private List<IHistoryProvider> _historyProviders = new();

        /// <summary>
        /// Gets the total number of data points emitted by this history provider
        /// </summary>
        public override int DataPointCount => GetDataPointCount();

        /// <summary>
        /// Sets the brokerage to be used for historical requests
        /// </summary>
        /// <param name="brokerage">The brokerage instance</param>
        public void SetBrokerage(IBrokerage brokerage)
        {
            _brokerage = brokerage;
        }

        /// <summary>
        /// Initializes this history provider to work for the specified job
        /// </summary>
        /// <param name="parameters">The initialization parameters</param>
        public override void Initialize(HistoryProviderInitializeParameters parameters)
        {
            if (_initialized)
            {
                // let's make sure no one tries to change our parameters values
                throw new InvalidOperationException("BrokerageHistoryProvider can only be initialized once");
            }
            _initialized = true;

            var dataProvidersList = parameters.Job?.HistoryProvider.DeserializeList() ?? new List<string>();
            if (dataProvidersList.IsNullOrEmpty())
            {
                dataProvidersList.AddRange(Config.Get("history-provider", "SubscriptionDataReaderHistoryProvider").DeserializeList());
            }

            foreach (var historyProviderName in dataProvidersList)
            {
                // Agy: it is wrong to use the Global.Instance.HistoryProvider. Because.
                // 1. That is used by other code parts for global historical price query. We don't want to use that for backtests.
                // 2. Because we have many CPU cores, we should run these Backtest of different users totally separately, without using global variables.
                // var historyProvider = Composer.Instance.GetExportedValueByTypeName<IHistoryProvider>(historyProviderName);
                SynchronizingHistoryProvider historyProvider;
                if (historyProviderName == "QuantConnect.Lean.Engine.HistoricalData.SubscriptionDataReaderHistoryProvider")
                    historyProvider = new SubscriptionDataReaderHistoryProvider();
                else
                    historyProvider = new BrokerageHistoryProvider(); 

                if (historyProvider is BrokerageHistoryProvider)
                {
                    (historyProvider as BrokerageHistoryProvider).SetBrokerage(_brokerage);
                }
                historyProvider.Initialize(parameters);
                historyProvider.InvalidConfigurationDetected += (sender, args) => { OnInvalidConfigurationDetected(args); };
                historyProvider.NumericalPrecisionLimited += (sender, args) => { OnNumericalPrecisionLimited(args); };
                historyProvider.StartDateLimited += (sender, args) => { OnStartDateLimited(args); };
                historyProvider.DownloadFailed += (sender, args) => { OnDownloadFailed(args); };
                historyProvider.ReaderErrorDetected += (sender, args) => { OnReaderErrorDetected(args); };
                _historyProviders.Add(historyProvider);
            }

            Utils.Logger.Trace($"HistoryProviderManager.Initialize(): history providers [{string.Join(",", _historyProviders.Select(x => x.GetType().Name))}]");
        }

        /// <summary>
        /// Gets the history for the requested securities
        /// </summary>
        /// <param name="requests">The historical data requests</param>
        /// <param name="sliceTimeZone">The time zone used when time stamping the slice instances</param>
        /// <returns>An enumerable of the slices of data covering the span specified in each request</returns>
        public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
        {
            List<IEnumerator<Slice>> historyEnumerators = new(_historyProviders.Count);
            var historyRequets = requests.ToList();
            foreach (var historyProvider in _historyProviders)
            {
                try
                {
                    var history = historyProvider.GetHistory(historyRequets, sliceTimeZone);
                    historyEnumerators.Add(history.GetEnumerator());
                }
                catch (Exception e)
                {
                    // ignore
                }
            }
            using var synchronizer = new SynchronizingSliceEnumerator(historyEnumerators);
            Slice latestMergeSlice = null;
            while (synchronizer.MoveNext())
            {
                if (synchronizer.Current == null)
                {
                    continue;
                }
                if (latestMergeSlice == null)
                {
                    latestMergeSlice = synchronizer.Current;
                    continue;
                }
                if (synchronizer.Current.UtcTime > latestMergeSlice.UtcTime)
                {
                    // a newer slice we emit the old and keep a reference of the new
                    // so in the next loop we merge if required
                    yield return latestMergeSlice;
                    latestMergeSlice = synchronizer.Current;
                }
                else
                {
                    // a new slice with same time we merge them into 'latestMergeSlice'
                    latestMergeSlice.MergeSlice(synchronizer.Current);
                }
            }
            if (latestMergeSlice != null)
            {
                yield return latestMergeSlice;
            }
        }

        private int GetDataPointCount()
        {
            var dataPointCount = 0;
            foreach (var historyProvider in _historyProviders)
            {
                dataPointCount += historyProvider.DataPointCount;
            }
            return dataPointCount;
        }
    }
}
