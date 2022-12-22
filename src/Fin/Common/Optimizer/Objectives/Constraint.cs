using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Util;
using System;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace QuantConnect.Optimizer.Objectives
{
    /// <summary>
    /// A backtest optimization constraint.
    /// Allows specifying statistical constraints for the optimization, eg. a backtest can't have a DrawDown less than 10%
    /// </summary>
    public class Constraint : Objective
    {
        /// <summary>
        /// The target comparison operation, eg. 'Greater'
        /// </summary>
        [JsonProperty("operator"), JsonConverter(typeof(StringEnumConverter), typeof(DefaultNamingStrategy))]
        public ComparisonOperatorTypes Operator { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public Constraint(string target, ComparisonOperatorTypes @operator, decimal? targetValue) : base(target, targetValue)
        {
            Operator = @operator;

            if (!TargetValue.HasValue)
            {
                throw new ArgumentNullException(nameof(targetValue), $"Constraint target value is not specified");
            }
        }

        /// <summary>
        /// Asserts the constraint is met
        /// </summary>
        public bool IsMet(string jsonBacktestResult)
        {
            if (string.IsNullOrEmpty(jsonBacktestResult))
            {
                throw new ArgumentNullException(nameof(jsonBacktestResult), "Constraint.IsMet: backtest result can not be null or empty.");
            }

            var token = JObject.Parse(jsonBacktestResult).SelectToken(Target);
            if (token == null)
            {
                return false;
            }

            return Operator.Compare(
                token.Value<string>().ToNormalizedDecimal(),
                TargetValue.Value);
        }

        /// <summary>
        /// Pretty representation of a constraint
        /// </summary>
        public override string ToString()
        {
            return $"{Target} '{Operator}' {TargetValue.Value}";
        }
    }
}
