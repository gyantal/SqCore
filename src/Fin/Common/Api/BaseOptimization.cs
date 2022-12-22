using System;
using Newtonsoft.Json;
using QuantConnect.Optimizer;
using QuantConnect.Optimizer.Objectives;

namespace QuantConnect.Api
{
    /// <summary>
    /// BaseOptimization item from the QuantConnect.com API.
    /// </summary>
    public class BaseOptimization : RestResponse
    {
        /// <summary>
        /// Optimization ID
        /// </summary>
        [JsonProperty(PropertyName = "optimizationId")]
        public string OptimizationId { get; set; }

        /// <summary>
        /// Project ID of the project the optimization belongs to
        /// </summary>
        [JsonProperty(PropertyName = "projectId")]
        public int ProjectId { get; set; }

        /// <summary>
        /// Name of the optimization
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Status of the optimization
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public OptimizationStatus Status { get; set; }

        /// <summary>
        /// Optimization node type
        /// </summary>
        /// <remarks><see cref="OptimizationNodes"/></remarks>
        [JsonProperty(PropertyName = "nodeType")]
        public string NodeType { get; set; }

        /// <summary>
        /// Optimization statistical target
        /// </summary>
        [JsonProperty(PropertyName = "criterion")]
        public Target Criterion { get; set; }
    }
}
