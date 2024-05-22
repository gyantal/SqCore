using System;

namespace MathCommon.MathNet;

/// <summary>
/// Statistics operating on an array already sorted ascendingly.
/// </summary>
/// <seealso cref="ArrayStatistics"/>
/// <seealso cref="StreamingStatistics"/>
/// <seealso cref="Statistics"/>
public static partial class SortedArrayStatistics
{
    /// <summary>
    /// Estimates the quantile tau from the sorted data array (ascending).
    /// The tau-th quantile is the data value where the cumulative distribution
    /// function crosses tau. The quantile definition can be specified to be compatible
    /// with an existing system.
    /// </summary>
    /// <param name="data">The data sample sequence.</param>
    /// <param name="x">Quantile value.</param>
    /// <param name="definition">Rank definition, to choose how ties should be handled and what product/definition it should be consistent with</param>
    public static double QuantileRank(double[] data, double x, RankDefinition definition = RankDefinition.Default)
    {
        if (x < data[0])
        {
            return 0.0;
        }

        if (x >= data[data.Length - 1])
        {
            return 1.0;
        }

        int right = Array.BinarySearch(data, x);
        if (right >= 0)
        {
            int left = right;

            while (left > 0 && data[left - 1] == data[left])
            {
                left--;
            }

            while (right < data.Length - 1 && data[right + 1] == data[right])
            {
                right++;
            }

            switch (definition)
            {
                case RankDefinition.EmpiricalCDF:
                    return (right + 1) / (double)data.Length;

                case RankDefinition.Max:
                    return right / (double)(data.Length - 1);

                case RankDefinition.Min:
                    return left / (double)(data.Length - 1);

                case RankDefinition.Average:
                    return (left / (double)(data.Length - 1) + right / (double)(data.Length - 1)) / 2;

                default:
                    throw new NotSupportedException();
            }
        }
        else
        {
            right = ~right;
            int left = right - 1;

            switch (definition)
            {
                case RankDefinition.EmpiricalCDF:
                    return (left + 1) / (double)data.Length;

                default:
                    {
                        var a = left / (double)(data.Length - 1);
                        var b = right / (double)(data.Length - 1);
                        return ((data[right] - x) * a + (x - data[left]) * b) / (data[right] - data[left]);
                    }
            }
        }
    }
}