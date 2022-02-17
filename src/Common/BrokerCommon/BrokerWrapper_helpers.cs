using IBApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BrokerCommon
{
    
    public class PriceAndTime      // it makes sense to store Time, because values older than X hours cannot be used, even if they are stored in the RAM
    {
        public double Price = Double.NaN;
        public DateTime Time = new(1950, 1, 1);    // a very old date

        public bool IsTimeUnchanged()
        {
            return Time == new DateTime(1950, 1, 1);
        }
    }

    public class MktDataSubscription
    {
        public delegate void MktDataArrivedFunc(int p_mktDataId, MktDataSubscription p_mktDataSubscription, int p_tickType, double p_price);
        public MktDataArrivedFunc? MarketDataArrived;

        public delegate void MktDataErrorFunc(int p_mktDataId, MktDataSubscription p_mktDataSubscription, int errorCode, string errorMsg);
        public MktDataErrorFunc? MarketDataError;

        public delegate void MktDataTickGenericFunc(int p_mktDataId, MktDataSubscription p_mktDataSubscription, int field, double value);
        public MktDataTickGenericFunc? MarketDataTickGeneric;

        public delegate void MktDataTypeFunc(int p_mktDataId, MktDataSubscription p_mktDataSubscription, int marketDataType);
        public MktDataTypeFunc? MarketDataType;

        public delegate void MktDataTickOptionComputationFunc(int p_mktDataId, MktDataSubscription p_mktDataSubscription, int field, double iv, double delta, double undPrice);
        public MktDataTickOptionComputationFunc? MarketDataTickOptionComputation;

        public Contract Contract { get; set; }
        public int MarketDataId { get; set; }
        public bool IsSnapshot { get; set; } = true; // Snapshot is simpler then Stream
        public bool IsAnyPriceArrived { get; set; }
        public int PreviousMktDataType { get; set; } = -1;
        public Timer? CheckDataIsAliveTimer { get; set; }

        //public AutoResetEvent m_priceTickARE = new AutoResetEvent(false);

        //public int MktDataTickerID { get; set; }

        public ConcurrentDictionary<int, PriceAndTime> Prices { get; set; }
        // Do we get this value every second really?, even if no new Ask or Bid Price has changed? If that is true, it is a good validator that we have the current price data or not.
        // If we don't get TimeStamp every second, we can use the Time value in the PriceAndTime() to know when did we received the data.
        // This tick represents "timestamp of the last Last tick" value in seconds (counted from 00:00:00 1 Jan 1970 UTC).  Value: 1457126686, which is a UNIX timestamp epoch: https://www.epochconverter.com/  seconds since Jan 01 1970. (UTC)
        public string LastTimestampStr { get; set; } = "0";   // store it quickly in i String, it arrives in a string. Do not process it unnecessarily.  

        public MktDataSubscription(Contract p_contract)
        {
            Contract = p_contract;
            Prices = new ConcurrentDictionary<int, PriceAndTime>();
            Prices.TryAdd(TickType.BID, new PriceAndTime());        // we are interested in the following Prices
            Prices.TryAdd(TickType.ASK, new PriceAndTime());
            Prices.TryAdd(TickType.LAST, new PriceAndTime());

            Prices.TryAdd(TickType.CLOSE, new PriceAndTime());      // previous day close
            Prices.TryAdd(TickType.OPEN, new PriceAndTime());
            Prices.TryAdd(TickType.HIGH, new PriceAndTime());
            Prices.TryAdd(TickType.LOW, new PriceAndTime());
        }
    }

    public class HistDataSubscription
    {
        public Contract Contract { get; set; }
        public AutoResetEvent AutoResetEvent { get; set; } = new AutoResetEvent(false);
        public List<QuoteData> QuoteData { get; set; } = new List<QuoteData>();

        public HistDataSubscription(Contract contract)
        {
            Contract = contract;
        }
    }


    public enum OrderStatus
    {
        /// <summary>
        /// indicates that you have transmitted the order, but have not yet received
        /// confirmation that it has been accepted by the order destination.
        /// This order status is not sent by TWS and should be explicitly set by the API developer when an order is submitted.
        /// </summary>
        PendingSubmit,
        /// <summary>
        /// PendingCancel - indicates that you have sent a request to cancel the order
        /// but have not yet received cancel confirmation from the order destination.
        /// At this point, your order is not confirmed canceled. You may still receive
        /// an execution while your cancellation request is pending.
        /// This order status is not sent by TWS and should be explicitly set by the API developer when an order is canceled.
        /// </summary>
        PendingCancel,
        /// <summary>
        /// indicates that a simulated order type has been accepted by the IB system and
        /// that this order has yet to be elected. The order is held in the IB system
        /// (and the status remains DARK BLUE) until the election criteria are met.
        /// At that time the order is transmitted to the order destination as specified
        /// (and the order status color will change).
        /// </summary>
        PreSubmitted,
        /// <summary>
        /// indicates that your order has been accepted at the order destination and is working.
        /// </summary>
        Submitted,
        /// <summary>
        /// indicates that the balance of your order has been confirmed canceled by the IB system.
        /// This could occur unexpectedly when IB or the destination has rejected your order.
        /// </summary>
        Canceled,
        /// <summary>
        /// The order has been completely filled.
        /// </summary>
        Filled,
        /// <summary>
        /// The Order is inactive
        /// </summary>
        Inactive,
        /// <summary>
        /// The order is Partially Filled
        /// </summary>
        PartiallyFilled,
        /// <summary>
        /// Api Pending
        /// </summary>
        ApiPending,
        /// <summary>
        /// Api Cancelled
        /// </summary>
        ApiCancelled,
        /// <summary>
        /// Indicates that there is an error with this order
        /// This order status is not sent by TWS and should be explicitly set by the API developer when an error has occured.
        /// </summary>
        Error,
        /// <summary>
        /// No Order Status
        /// </summary>
        MinFilterSkipped,
        MaxFilterSkipped,
        Unrecognized,
        None
    }

    public class OrderSubscription
    {
        public Contract Contract { get; set; }
        public Order Order { get; set; }
        public AutoResetEvent AutoResetEvent { get; set; } = new AutoResetEvent(false);

        public DateTime DateTime { get; set; }
        public OrderStatus OrderStatus { get; set; } = OrderStatus.None;
        public double Filled { get; set; }
        public double Remaining { get; set; }
        public double AvgFillPrice { get; set; }
        public int PermId { get; set; }
        public int ParentId { get; set; }
        public double LastFillPrice { get; set; }
        public int ClientId { get; set; }
        public string WhyHeld { get; set; } = string.Empty;

        public OrderSubscription(Contract contract, Order order)
        {
            Contract = contract;
            Order = order;
        }
    }


    public interface IBrokerWrapper : EWrapper
    {
        string IbAccountsList { get; set; }

        bool Connect(GatewayId p_gatewayUser, string host, int p_socketPort, int p_brokerConnectionClientID);
        void Disconnect();
        bool IsConnected();

        int ReqAccountSummary();
        void CancelAccountSummary(int p_reqId);
        void ReqPositions();

        // snapshot = true means, Last, Ask,Bid,High, Low, Close, Open comes only once. No streaming. No need to CancelMktData().
        // if data is streamed continously and then we ask one snapshot of the same contract, snapshot returns currently, and stream also correctly continous later. As expected.
        int ReqMktDataStream(Contract p_contract, string p_genericTickList = "", bool p_snapshot = false, MktDataSubscription.MktDataArrivedFunc? p_mktDataArrivedFunc = null, MktDataSubscription.MktDataErrorFunc? p_mktDataErrorFunc = null, MktDataSubscription.MktDataTickGenericFunc? p_mktDataTickGenericFunc = null, MktDataSubscription.MktDataTypeFunc? p_mktDataTypeFunc = null, MktDataSubscription.MktDataTickOptionComputationFunc? p_mktDataTickOptionComputationFunc = null);
        void CancelMktData(int p_marketDataId);
        bool GetAlreadyStreamedPrice(Contract p_contract, ref Dictionary<int, PriceAndTime> p_quotes);

        bool ReqHistoricalData(DateTime p_endDateTime, int p_lookbackWindowSize, string p_whatToShow, Contract p_contract, out List<QuoteData>? p_quotes);

        int PlaceOrder(Contract p_contract, TransactionType p_transactionType, double p_volume, OrderExecution p_orderExecution, OrderTimeInForce p_orderTif, double? p_limitPrice, double? p_stopPrice, double p_estimatedPrice, bool p_isSimulatedTrades);
        bool WaitOrder(int p_realOrderId, bool p_isSimulatedTrades);
        bool GetRealOrderExecutionInfo(int p_realOrderId, ref OrderStatus p_realOrderStatus, ref double p_realExecutedVolume, ref double p_realExecutedAvgPrice, ref DateTime p_execptionTime, bool p_isSimulatedTrades);
    }
}
