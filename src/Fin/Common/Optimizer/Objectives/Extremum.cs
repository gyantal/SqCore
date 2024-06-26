﻿using System;
using Newtonsoft.Json;

namespace QuantConnect.Optimizer.Objectives
{
    /// <summary>
    /// Define the way to compare current real-values and the new one (candidates).
    /// It's encapsulated in different abstraction to allow configure the direction of optimization, i.e. max or min.
    /// </summary>
    [JsonConverter(typeof(ExtremumJsonConverter))]
    public class Extremum
    {
        private Func<decimal, decimal, bool> _comparer;

        /// <summary>
        /// Create an instance of <see cref="Extremum"/> to compare values.
        /// </summary>
        /// <param name="comparer">The way old and new values should be compared</param>
        public Extremum(Func<decimal, decimal, bool> comparer)
        {
            _comparer = comparer;
        }

        /// <summary>
        /// Compares two values; identifies whether condition is met or not.
        /// </summary>
        /// <param name="current">Left operand</param>
        /// <param name="candidate">Right operand</param>
        /// <returns>Returns the result of comparer with this arguments</returns>
        public bool Better(decimal current, decimal candidate) => _comparer(current, candidate);
    }
}
