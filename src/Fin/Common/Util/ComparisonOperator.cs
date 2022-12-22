using System;

namespace QuantConnect.Util
{
    /// <summary>
    /// Utility Comparison Operator class
    /// </summary>
    public static class ComparisonOperator
    {
        /// <summary>
        /// Compares two values using given operator
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="op">Comparison operator</param>
        /// <param name="arg1">The first value</param>
        /// <param name="arg2">The second value</param>
        /// <returns>Returns true if its left-hand operand meets the operator value to its right-hand operand, false otherwise</returns>
        public static bool Compare<T>(ComparisonOperatorTypes op, T arg1, T arg2) where T : IComparable
        {
            switch (op)
            {
                case ComparisonOperatorTypes.Equals:
                    return arg1.CompareTo(arg2) == 0;
                case ComparisonOperatorTypes.NotEqual:
                    return arg1.CompareTo(arg2) != 0;
                case ComparisonOperatorTypes.Greater:
                    return arg1.CompareTo(arg2) == 1;
                case ComparisonOperatorTypes.GreaterOrEqual:
                    return arg1.CompareTo(arg2) >= 0;
                case ComparisonOperatorTypes.Less:
                    return arg1.CompareTo(arg2) == -1;
                case ComparisonOperatorTypes.LessOrEqual:
                    return arg1.CompareTo(arg2) <= 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), $"Operator '{op}' is not supported.");
            }
        }
    }
}
