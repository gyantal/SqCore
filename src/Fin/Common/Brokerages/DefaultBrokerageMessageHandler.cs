using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Util;
using SqCommon;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Provides a default implementation o <see cref="IBrokerageMessageHandler"/> that will forward
    /// messages as follows:
    /// Information -> IResultHandler.Debug
    /// Warning     -> IResultHandler.Error &amp;&amp; IApi.SendUserEmail
    /// Error       -> IResultHandler.Error &amp;&amp; IAlgorithm.RunTimeError
    /// </summary>
    public class DefaultBrokerageMessageHandler : IBrokerageMessageHandler
    {
        private static readonly TimeSpan DefaultOpenThreshold = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromMinutes(15);

        private volatile bool _connected;

        private readonly IApi _api;
        private readonly IAlgorithm _algorithm;
        private readonly TimeSpan _openThreshold;
        private readonly AlgorithmNodePacket _job;
        private readonly TimeSpan _initialDelay;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultBrokerageMessageHandler"/> class
        /// </summary>
        /// <param name="algorithm">The running algorithm</param>
        /// <param name="job">The job that produced the algorithm</param>
        /// <param name="api">The api for the algorithm</param>
        /// <param name="initialDelay"></param>
        /// <param name="openThreshold">Defines how long before market open to re-check for brokerage reconnect message</param>
        public DefaultBrokerageMessageHandler(IAlgorithm algorithm, AlgorithmNodePacket job, IApi api, TimeSpan? initialDelay = null, TimeSpan? openThreshold = null)
        {
            _api = api;
            _job = job;
            _algorithm = algorithm;
            _connected = true;
            _openThreshold = openThreshold ?? DefaultOpenThreshold;
            _initialDelay = initialDelay ?? DefaultInitialDelay;
        }

        /// <summary>
        /// Handles the message
        /// </summary>
        /// <param name="message">The message to be handled</param>
        public void Handle(BrokerageMessageEvent message)
        {
            // based on message type dispatch to result handler
            switch (message.Type)
            {
                case BrokerageMessageType.Information:
                    _algorithm.Debug($"Brokerage Info: {message.Message}");
                    break;

                case BrokerageMessageType.Warning:
                    _algorithm.Error($"Brokerage Warning: {message.Message}");
                    break;

                case BrokerageMessageType.Error:
                    // unexpected error, we need to close down shop
                    _algorithm.SetRuntimeError(new Exception(message.Message), "Brokerage Error");
                    break;

                case BrokerageMessageType.Disconnect:
                    _connected = false;
                    Utils.Logger.Trace("DefaultBrokerageMessageHandler.Handle(): Disconnected.");

                    // check to see if any non-custom security exchanges are open within the next x minutes
                    var open = (from kvp in _algorithm.Securities
                                let security = kvp.Value
                                where security.Type != SecurityType.Base
                                let exchange = security.Exchange
                                let localTime = _algorithm.UtcTime.ConvertFromUtc(exchange.TimeZone)
                                where exchange.IsOpenDuringBar(
                                    localTime,
                                    localTime + _openThreshold,
                                    _algorithm.SubscriptionManager.SubscriptionDataConfigService
                                        .GetSubscriptionDataConfigs(security.Symbol)
                                        .IsExtendedMarketHours())
                                select security).Any();

                    // if any are open then we need to kill the algorithm
                    if (open)
                    {
                        Utils.Logger.Trace("DefaultBrokerageMessageHandler.Handle(): Disconnect when exchanges are open, " +
                            Invariant($"trying to reconnect for {_initialDelay.TotalMinutes} minutes.")
                        );

                        // wait 15 minutes before killing algorithm
                        StartCheckReconnected(_initialDelay, message);
                    }
                    else
                    {
                        Utils.Logger.Trace("DefaultBrokerageMessageHandler.Handle(): Disconnect when exchanges are closed, checking back before exchange open.");

                        // if they aren't open, we'll need to check again a little bit before markets open
                        DateTime nextMarketOpenUtc;
                        if (_algorithm.Securities.Count != 0)
                        {
                            nextMarketOpenUtc = (from kvp in _algorithm.Securities
                                                 let security = kvp.Value
                                                 where security.Type != SecurityType.Base
                                                 let exchange = security.Exchange
                                                 let localTime = _algorithm.UtcTime.ConvertFromUtc(exchange.TimeZone)
                                                 let marketOpen = exchange.Hours.GetNextMarketOpen(localTime,
                                                     _algorithm.SubscriptionManager.SubscriptionDataConfigService
                                                         .GetSubscriptionDataConfigs(security.Symbol)
                                                         .IsExtendedMarketHours())
                                                 let marketOpenUtc = marketOpen.ConvertToUtc(exchange.TimeZone)
                                                 select marketOpenUtc).Min();
                        }
                        else
                        {
                            // if we have no securities just make next market open an hour from now
                            nextMarketOpenUtc = DateTime.UtcNow.AddHours(1);
                        }

                        var timeUntilNextMarketOpen = nextMarketOpenUtc - DateTime.UtcNow - _openThreshold;
                        Utils.Logger.Trace(Invariant($"DefaultBrokerageMessageHandler.Handle(): TimeUntilNextMarketOpen: {timeUntilNextMarketOpen}"));

                        // wake up 5 minutes before market open and check if we've reconnected
                        StartCheckReconnected(timeUntilNextMarketOpen, message);
                    }
                    break;

                case BrokerageMessageType.Reconnect:
                    _connected = true;
                    Utils.Logger.Trace("DefaultBrokerageMessageHandler.Handle(): Reconnected.");

                    if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    break;
            }
        }

        private void StartCheckReconnected(TimeSpan delay, BrokerageMessageEvent message)
        {
            _cancellationTokenSource.DisposeSafely();
            _cancellationTokenSource = new CancellationTokenSource(delay);

            Task.Run(() =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                }

                CheckReconnected(message);

            }, _cancellationTokenSource.Token);
        }

        private void CheckReconnected(BrokerageMessageEvent message)
        {
            if (!_connected)
            {
                Utils.Logger.Error("DefaultBrokerageMessageHandler.Handle(): Still disconnected, goodbye.");
                _algorithm.SetRuntimeError(new Exception(message.Message), "Brokerage Disconnect");
            }
        }
    }
}
