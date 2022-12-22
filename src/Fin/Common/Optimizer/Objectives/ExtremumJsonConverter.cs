using System;
using QuantConnect.Util;

namespace QuantConnect.Optimizer.Objectives
{
    public class ExtremumJsonConverter : TypeChangeJsonConverter<Extremum, string>
    {
        /// <summary>
        /// Don't populate any property
        /// </summary>
        protected override bool PopulateProperties => false;

        protected override string Convert(Extremum value)
        {
            return value.GetType() == typeof(Maximization)
                ? "max"
                : "min";
        }

        protected override Extremum Convert(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "max": return new Maximization();
                case "min": return new Minimization();
                default:
                    throw new InvalidOperationException("ExtremumJsonConverter.Convert: could not recognize target direction");
            }
        }
    }
}
