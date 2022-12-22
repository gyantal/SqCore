using Newtonsoft.Json;
using QuantConnect.Interfaces;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Contains additional properties and settings for an order
    /// </summary>
    public class OrderProperties : IOrderProperties
    {
        /// <summary>
        /// Defines the length of time over which an order will continue working before it is cancelled
        /// </summary>
        public TimeInForce TimeInForce { get; set; }

        /// <summary>
        /// Defines the exchange name for a particular market
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Exchange Exchange { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderProperties"/> class
        /// </summary>
        public OrderProperties()
        {
            TimeInForce = TimeInForce.GoodTilCanceled;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderProperties"/> class, with exchange param
        ///<param name="exchange">Exchange name for market</param>
        /// </summary>
        public OrderProperties(Exchange exchange) : this()
        {
            Exchange = exchange;
        }

        /// <summary>
        /// Returns a new instance clone of this object
        /// </summary>
        public virtual IOrderProperties Clone()
        {
            return (OrderProperties)MemberwiseClone();
        }
    }
}
