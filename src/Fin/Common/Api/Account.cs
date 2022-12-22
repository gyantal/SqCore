using System;
using Newtonsoft.Json;

namespace QuantConnect.Api
{
    /// <summary>
    /// Account information for an organization
    /// </summary>
    public class Account : RestResponse
    {
        /// <summary>
        /// The organization Id
        /// </summary>
        [JsonProperty(PropertyName = "organizationId")]
        public string OrganizationId { get; set; }

        /// <summary>
        /// The current account balance
        /// </summary>
        [JsonProperty(PropertyName = "creditBalance")]
        public decimal CreditBalance { get; set; }

        /// <summary>
        /// The current organizations credit card
        /// </summary>
        [JsonProperty(PropertyName = "card")]
        public Card Card { get; set; }
    }

    /// <summary>
    /// Credit card
    /// </summary>
    public class Card
    {
        /// <summary>
        /// Credit card brand
        /// </summary>
        [JsonProperty(PropertyName = "brand")]
        public string Brand { get; set; }

        /// <summary>
        /// The credit card expiration
        /// </summary>
        [JsonProperty(PropertyName = "expiration")]
        public DateTime Expiration { get; set; }

        /// <summary>
        /// The last 4 digits of the card
        /// </summary>
        [JsonProperty(PropertyName = "last4")]
        public decimal LastFourDigits { get; set; }
    }
}
