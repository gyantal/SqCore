using Newtonsoft.Json;
using QuantConnect.Interfaces;
using QuantConnect.Brokerages;

namespace QuantConnect
{
    /// <summary>
    /// This class includes algorithm configuration settings and parameters.
    /// This is used to include configuration parameters in the result packet to be used for report generation.
    /// </summary>
    public class AlgorithmConfiguration
    {
        /// <summary>
        /// The algorithm's account currency
        /// </summary>
        [JsonProperty(PropertyName = "AccountCurrency", NullValueHandling = NullValueHandling.Ignore)]
        public string AccountCurrency;

        /// <summary>
        /// The algorithm's brokerage model
        /// </summary>
        /// <remarks> Required to set the correct brokerage model on report generation.</remarks>
        [JsonProperty(PropertyName = "Brokerage")]
        public BrokerageName BrokerageName;

        /// <summary>
        /// The algorithm's account type
        /// </summary>
        /// <remarks> Required to set the correct brokerage model on report generation.</remarks>
        [JsonProperty(PropertyName = "AccountType")]
        public AccountType AccountType;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorithmConfiguration"/> class
        /// </summary>
        public AlgorithmConfiguration(string accountCurrency, BrokerageName brokerageName, AccountType accountType)
        {
            AccountCurrency = accountCurrency;
            BrokerageName = brokerageName;
            AccountType = accountType;
        }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="AlgorithmConfiguration"/> class
        /// </summary>
        public AlgorithmConfiguration()
        {
        }

        /// <summary>
        /// Provides a convenience method for creating a <see cref="AlgorithmConfiguration"/> for a given algorithm.
        /// </summary>
        /// <param name="algorithm">Algorithm for which the configuration object is being created</param>
        /// <returns>A new AlgorithmConfiguration object for the specified algorithm</returns>
        public static AlgorithmConfiguration Create(IAlgorithm algorithm)
        {
            return new AlgorithmConfiguration(
                algorithm.AccountCurrency,
                BrokerageModel.GetBrokerageName(algorithm.BrokerageModel),
                algorithm.BrokerageModel.AccountType);
        }
    }
}
