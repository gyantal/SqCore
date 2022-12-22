using QuantConnect.Util;

namespace QuantConnect.Orders.Serialization
{
    /// <summary>
    /// Defines how OrderEvents should be serialized to json
    /// </summary>
    public class OrderEventJsonConverter : TypeChangeJsonConverter<OrderEvent, SerializedOrderEvent>
    {
        private readonly string _algorithmId;

        /// <summary>
        /// True will populate TResult object returned by <see cref="Convert(SerializedOrderEvent)"/> with json properties
        /// </summary>
        protected override bool PopulateProperties => false;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="algorithmId">The associated algorithm id, required when serializing</param>
        public OrderEventJsonConverter(string algorithmId = null)
        {
            _algorithmId = algorithmId;
        }

        /// <summary>
        /// Convert the input value to a value to be serialzied
        /// </summary>
        /// <param name="value">The input value to be converted before serialziation</param>
        /// <returns>A new instance of TResult that is to be serialzied</returns>
        protected override SerializedOrderEvent Convert(OrderEvent value)
        {
            return new SerializedOrderEvent(value, _algorithmId);
        }

        /// <summary>
        /// Converts the input value to be deserialized
        /// </summary>
        /// <param name="value">The deserialized value that needs to be converted to <see cref="OrderEvent"/></param>
        /// <returns>The converted value</returns>
        protected override OrderEvent Convert(SerializedOrderEvent value)
        {
            return OrderEvent.FromSerialized(value);
        }
    }
}
