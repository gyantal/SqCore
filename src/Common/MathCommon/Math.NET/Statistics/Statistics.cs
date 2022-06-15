using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// https://github.com/mathnet/mathnet-numerics/blob/8291d7b619310a34cb17f30c428706a295dc11de/src/Numerics/Statistics/Statistics.cs
namespace MathCommon.MathNet
{
    public static class Statistics
    {
        /// <summary>
        /// Estimates the p-Percentile value from the provided samples.
        /// If a non-integer Percentile is needed, use Quantile instead.
        /// Approximately median-unbiased regardless of the sample distribution (R8).
        /// </summary>
        /// <param name="data">The data sample sequence.</param>
        /// <param name="p">Percentile selector, between 0 and 100 (inclusive).</param>
        public static double Percentile(this IEnumerable<double> data, int p)
        {
            double[] array = data.ToArray();
            return ArrayStatistics.PercentileInplace(array, p);
        }

        /// <summary>
        /// Estimates the tau-th quantile from the provided samples.
        /// The tau-th quantile is the data value where the cumulative distribution
        /// function crosses tau.
        /// Approximately median-unbiased regardless of the sample distribution (R8).
        /// </summary>
        /// <param name="data">The data sample sequence.</param>
        /// <param name="tau">Quantile selector, between 0.0 and 1.0 (inclusive).</param>
        public static double Quantile(this IEnumerable<double> data, double tau)
        {
            double[] array = data.ToArray();
            return ArrayStatistics.QuantileInplace(array, tau);
        }
    }
}