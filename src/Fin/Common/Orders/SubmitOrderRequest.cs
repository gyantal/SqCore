using System;
using QuantConnect.Interfaces;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Defines a request to submit a new order
    /// </summary>
    public class SubmitOrderRequest : OrderRequest
    {
        /// <summary>
        /// Gets <see cref="Orders.OrderRequestType.Submit"/>
        /// </summary>
        public override OrderRequestType OrderRequestType
        {
            get { return OrderRequestType.Submit; }
        }

        /// <summary>
        /// Gets the security type of the symbol
        /// </summary>
        public SecurityType SecurityType
        {
            get; private set;
        }

        /// <summary>
        /// Gets the symbol to be traded
        /// </summary>
        public Symbol Symbol
        {
            get; private set;
        }

        /// <summary>
        /// Gets the order type od the order
        /// </summary>
        public OrderType OrderType
        {
            get; private set;
        }

        /// <summary>
        /// Gets the quantity of the order
        /// </summary>
        public decimal Quantity
        {
            get; private set;
        }

        /// <summary>
        /// Gets the limit price of the order, zero if not a limit order
        /// </summary>
        public decimal LimitPrice
        {
            get; private set;
        }

        /// <summary>
        /// Gets the stop price of the order, zero if not a stop order
        /// </summary>
        public decimal StopPrice
        {
            get; private set;
        }

        /// <summary>
        /// Price which must first be reached before a limit order can be submitted.
        /// </summary>
        public decimal TriggerPrice
        {
            get; private set;
        }

        /// <summary>
        /// Gets the order properties for this request
        /// </summary>
        public IOrderProperties OrderProperties
        {
            get; private set;
        }

        // SqCore Change NEW:
        public decimal FixPrice
        {
            get; private set;
        } = decimal.MinValue;
        // SqCore Change END

        /// <summary>
        /// Initializes a new instance of the <see cref="SubmitOrderRequest"/> class.
        /// The <see cref="OrderRequest.OrderId"/> will default to <see cref="OrderResponseErrorCode.UnableToFindOrder"/>
        /// </summary>
        /// <param name="orderType">The order type to be submitted</param>
        /// <param name="securityType">The symbol's <see cref="SecurityType"/></param>
        /// <param name="symbol">The symbol to be traded</param>
        /// <param name="quantity">The number of units to be ordered</param>
        /// <param name="stopPrice">The stop price for stop orders, non-stop orers this value is ignored</param>
        /// <param name="limitPrice">The limit price for limit orders, non-limit orders this value is ignored</param>
        /// <param name="triggerPrice">The trigger price for limit if touched orders, for non-limit if touched orders this value is ignored</param>
        /// <param name="time">The time this request was created</param>
        /// <param name="tag">A custom tag for this request</param>
        /// <param name="properties">The order properties for this request</param>
        public SubmitOrderRequest(
            OrderType orderType,
            SecurityType securityType,
            Symbol symbol,
            decimal quantity,
            decimal stopPrice,
            decimal limitPrice,
            decimal triggerPrice,
            // SqCore Change NEW:
            decimal fixPrice, // We use  Decimal.MinValue for Invalid price. 0 can be a valid fix trade price for Options e.g.
            // SqCore Change END
            DateTime time,
            string tag,
            IOrderProperties properties = null
            )
            : base(time, (int) OrderResponseErrorCode.UnableToFindOrder, tag)
        {
            SecurityType = securityType;
            Symbol = symbol;
            OrderType = orderType;
            Quantity = quantity;
            LimitPrice = limitPrice;
            StopPrice = stopPrice;
            TriggerPrice = triggerPrice;
            OrderProperties = properties;
            // SqCore Change NEW:
            FixPrice = fixPrice;
            // SqCore Change END
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SubmitOrderRequest"/> class.
        /// The <see cref="OrderRequest.OrderId"/> will default to <see cref="OrderResponseErrorCode.UnableToFindOrder"/>
        /// </summary>
        /// <param name="orderType">The order type to be submitted</param>
        /// <param name="securityType">The symbol's <see cref="SecurityType"/></param>
        /// <param name="symbol">The symbol to be traded</param>
        /// <param name="quantity">The number of units to be ordered</param>
        /// <param name="stopPrice">The stop price for stop orders, non-stop orers this value is ignored</param>
        /// <param name="limitPrice">The limit price for limit orders, non-limit orders this value is ignored</param>
        /// <param name="time">The time this request was created</param>
        /// <param name="tag">A custom tag for this request</param>
        /// <param name="properties">The order properties for this request</param>
        public SubmitOrderRequest(
            OrderType orderType,
            SecurityType securityType,
            Symbol symbol,
            decimal quantity,
            decimal stopPrice,
            decimal limitPrice,
            // SqCore Change NEW:
            decimal fixPrice, // We use  Decimal.MinValue for Invalid price. 0 can be a valid fix trade price for Options e.g.
            // SqCore Change END
            DateTime time,
            string tag,
            IOrderProperties properties = null
            )
            // SqCore Change ORIGINAL:
            // : this(orderType, securityType, symbol, quantity, stopPrice, limitPrice, 0, time, tag, properties)
            // SqCore Change NEW:
            : this(orderType, securityType, symbol, quantity, stopPrice, limitPrice, 0, fixPrice, time, tag, properties)
            // SqCore Change END
        {
        }

        /// <summary>
        /// Sets the <see cref="OrderRequest.OrderId"/>
        /// </summary>
        /// <param name="orderId">The order id of the generated order</param>
        internal void SetOrderId(int orderId)
        {
            OrderId = orderId;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            // create a proxy order object to steal his to string method
            Order proxy = Order.CreateOrder(this);
            return Invariant($"{Time} UTC: Submit Order: ({OrderId}) - {proxy} {Tag} Status: {Status}");
        }
    }
}
