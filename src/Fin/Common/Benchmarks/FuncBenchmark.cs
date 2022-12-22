using System;
using Python.Runtime;

namespace QuantConnect.Benchmarks
{
    /// <summary>
    /// Creates a benchmark defined by a function
    /// </summary>
    public class FuncBenchmark : IBenchmark
    {
        private readonly Func<DateTime, decimal> _benchmark;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuncBenchmark"/> class
        /// </summary>
        /// <param name="benchmark">The functional benchmark implementation</param>
        public FuncBenchmark(Func<DateTime, decimal> benchmark)
        {
            if (benchmark == null)
            {
                throw new ArgumentNullException(nameof(benchmark));
            }
            _benchmark = benchmark;
        }

        /// <summary>
        /// Create a function benchmark from a Python function
        /// </summary>
        /// <param name="pyFunc"></param>
        public FuncBenchmark(PyObject pyFunc)
        {
            if (!pyFunc.TryConvertToDelegate(out _benchmark))
            {
                throw new ArgumentException("FuncBenchmark(): Unable to convert Python function to benchmark function," +
                    " please ensure the function supports Datetime input and decimal output");
            }
        }

        /// <summary>
        /// Evaluates this benchmark at the specified time
        /// </summary>
        /// <param name="time">The time to evaluate the benchmark at</param>
        /// <returns>The value of the benchmark at the specified time</returns>
        public decimal Evaluate(DateTime time)
        {
            return _benchmark(time);
        }
    }
}