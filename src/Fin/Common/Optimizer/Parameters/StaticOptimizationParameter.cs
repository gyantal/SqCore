using Newtonsoft.Json;

namespace QuantConnect.Optimizer.Parameters
{
    /// <summary>
    /// Defines the step based optimization parameter
    /// </summary>
    public class StaticOptimizationParameter : OptimizationParameter
    {
        /// <summary>
        /// Minimum value of optimization parameter, applicable for boundary conditions
        /// </summary>
        [JsonProperty("value")]
        public string Value { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="name">The name of the parameter</param>
        /// <param name="value">The fixed value of this parameter</param>
        public StaticOptimizationParameter(string name, string value) : base(name)
        {
            Value = value;
        }
    }
}
