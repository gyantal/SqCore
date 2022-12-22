using System.Collections.Generic;
using Newtonsoft.Json;
using QuantConnect.Api;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Collection container for a list of orders for a project
    /// </summary>
    public class OrdersResponseWrapper : RestResponse
    {
        /// <summary>
        /// Collection of summarized Orders objects
        /// </summary>
        [JsonProperty(PropertyName = "orders")]
        public List<Order> Orders { get; set; } = new();
    }
}
