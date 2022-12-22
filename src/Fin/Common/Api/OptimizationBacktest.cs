using System.Collections.Generic;
using Newtonsoft.Json;
using QuantConnect.Optimizer.Parameters;

namespace QuantConnect.Api
{
    /// <summary>
    /// OptimizationBacktest object from the QuantConnect.com API.
    /// </summary>
    [JsonConverter(typeof(OptimizationBacktestJsonConverter))]
    public class OptimizationBacktest
    {
        /// <summary>
        /// Progress of the backtest as a percentage from 0-1 based on the days lapsed from start-finish.
        /// </summary>
        public decimal Progress { get; set; }

        /// <summary>
        /// The backtest name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The backtest host name
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// The backtest id
        /// </summary>
        public string BacktestId { get; }

        /// <summary>
        /// Represent a combination as key value of parameters, i.e. order doesn't matter
        /// </summary>
        public ParameterSet ParameterSet { get; }

        /// <summary>
        /// The backtest statistics results
        /// </summary>
        public IDictionary<string, string> Statistics { get; set; }

        /// <summary>
        /// The backtest equity chart series
        /// </summary>
        public Series Equity { get; set; }

        /// <summary>
        /// The exit code of this backtest
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="parameterSet">The parameter set</param>
        /// <param name="backtestId">The backtest id if any</param>
        /// <param name="name">The backtest name</param>
        public OptimizationBacktest(ParameterSet parameterSet, string backtestId, string name)
        {
            ParameterSet = parameterSet;
            BacktestId = backtestId;
            Name = name;
        }
    }
}
