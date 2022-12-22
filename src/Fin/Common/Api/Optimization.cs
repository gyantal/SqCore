using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuantConnect.Optimizer.Objectives;
using QuantConnect.Optimizer.Parameters;

namespace QuantConnect.Api
{
    /// <summary>
    /// Optimization response packet from the QuantConnect.com API.
    /// </summary>
    public class Optimization : BaseOptimization
    {
        /// <summary>
        /// Runtime banner/updating statistics for the optimization
        /// </summary>
        [JsonProperty(PropertyName = "runtimeStatistics", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> RuntimeStatistics { get; set; }

        /// <summary>
        /// Optimization constraints
        /// </summary>
        [JsonProperty(PropertyName = "constraints", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<Constraint> Constraints { get; set; }

        /// <summary>
        /// Optimization parameters
        /// </summary>
        [JsonProperty(PropertyName = "parameters", NullValueHandling = NullValueHandling.Ignore)]
        public HashSet<OptimizationParameter> Parameters { get; set; }

        /// <summary>
        /// Number of parallel nodes for optimization
        /// </summary>
        [JsonProperty(PropertyName = "parallelNodes")]
        public int ParallelNodes { get; set; }

        /// <summary>
        /// Optimization constraints
        /// </summary>
        [JsonProperty(PropertyName = "backtests", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, OptimizationBacktest> Backtests { get; set; }

        /// <summary>
        /// Optimization strategy
        /// </summary>
        [JsonProperty(PropertyName = "strategy")]
        public string Strategy { get; set; }
        
        /// <summary>
        /// Optimization requested date and time
        /// </summary>
        [JsonProperty(PropertyName = "requested")]
        public DateTime Requested { get; set; }
    }

    /// <summary>
    /// Wrapper class for Optimizations/Read endpoint JSON response
    /// </summary>
    public class OptimizationResponseWrapper : RestResponse
    {
        /// <summary>
        /// Optimization object
        /// </summary>
        [JsonProperty(PropertyName = "optimization")]
        public Optimization Optimization { get; set; }
    }

    /// <summary>
    /// Collection container for a list of summarized optimizations for a project
    /// </summary>
    public class OptimizationList : RestResponse
    {
        /// <summary>
        /// Collection of summarized optimization objects
        /// </summary>
        [JsonProperty(PropertyName = "optimizations")]
        public List<BaseOptimization> Optimizations { get; set; }
    }
}
