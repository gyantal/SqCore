using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Api
{
    /// <summary>
    /// Live algorithm instance result from the QuantConnect Rest API.
    /// </summary>
    public class LiveAlgorithm : RestResponse
    {
        /// <summary>
        /// Project id for the live instance
        /// </summary>
        [JsonProperty(PropertyName = "projectId")]
        public int ProjectId { get; set; }

        /// <summary>
        /// Unique live algorithm deployment identifier (similar to a backtest id).
        /// </summary>
        [JsonProperty(PropertyName = "deployId")]
        public string DeployId { get; set; }

        /// <summary>
        /// Algorithm status: running, stopped or runtime error.
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public AlgorithmStatus Status { get; set; }

        /// <summary>
        /// Datetime the algorithm was launched in UTC.
        /// </summary>
        [JsonProperty(PropertyName = "launched")]
        public DateTime Launched { get; set; }

        /// <summary>
        /// Datetime the algorithm was stopped in UTC, null if its still running.
        /// </summary>
        [JsonProperty(PropertyName = "stopped")]
        public DateTime? Stopped { get; set; }

        /// <summary>
        /// Brokerage
        /// </summary>
        [JsonProperty(PropertyName = "brokerage")]
        public string Brokerage { get; set; }

        /// <summary>
        /// Chart we're subscribed to
        /// </summary>
        /// <remarks>
        /// Data limitations mean we can only stream one chart at a time to the consumer. See which chart you're watching here.
        /// </remarks>
        [JsonProperty(PropertyName = "subscription")]
        public string Subscription { get; set; }

        /// <summary>
        /// Live algorithm error message from a crash or algorithm runtime error.
        /// </summary>
        [JsonProperty(PropertyName = "error")]
        public string Error { get; set; }
    }

    /// <summary>
    /// List of the live algorithms running which match the requested status
    /// </summary>
    public class LiveList : RestResponse
    {
        /// <summary>
        /// Algorithm list matching the requested status.
        /// </summary>
        [JsonProperty(PropertyName = "live")]
        public List<LiveAlgorithm> Algorithms { get; set; }
    }
}
