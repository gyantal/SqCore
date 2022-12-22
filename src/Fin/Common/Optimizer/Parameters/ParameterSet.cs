using System.Linq;
using QuantConnect.Util;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuantConnect.Api;

namespace QuantConnect.Optimizer.Parameters
{
    /// <summary>
    /// Represents a single combination of optimization parameters
    /// </summary>
    [JsonConverter(typeof(ParameterSetJsonConverter))]
    public class ParameterSet
    {
        /// <summary>
        /// The unique identifier within scope (current optimization job)
        /// </summary>
        /// <remarks>Internal id, useful for the optimization strategy to id each generated parameter sets,
        /// even before there is any backtest id</remarks>
        [JsonProperty(PropertyName = "id")]
        public int Id { get; }

        /// <summary>
        /// Represent a combination as key value of parameters, i.e. order doesn't matter
        /// </summary>
        [JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, string> Value { get; }

        /// <summary>
        /// Creates an instance of <see cref="ParameterSet"/> based on new combination of optimization parameters
        /// </summary>
        /// <param name="id">Unique identifier</param>
        /// <param name="value">Combination of optimization parameters</param>
        public ParameterSet(int id, Dictionary<string, string> value)
        {
            Id = id;
            Value = value?.ToReadOnlyDictionary();
        }

        /// <summary>
        /// String representation of this parameter set
        /// </summary>
        public override string ToString()
        {
            return string.Join(",", Value.Select(arg => $"{arg.Key}:{arg.Value}"));
        }
    }
}
