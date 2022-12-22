using System;

namespace QuantConnect.Benchmarks
{
    /// <summary>
    /// Specifies how to compute a benchmark for an algorithm
    /// </summary>
    public interface IBenchmark
    {
        /// <summary>
        /// Evaluates this benchmark at the specified time
        /// </summary>
        /// <param name="time">The time to evaluate the benchmark at</param>
        /// <returns>The value of the benchmark at the specified time</returns>
        decimal Evaluate(DateTime time);
    }
}
