using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MathCommon.MathNet
{
    public static partial class SpecialFunctions
    {
        /// <summary>
        /// Returns the regularized lower incomplete beta function
        /// I_x(a,b) = 1/Beta(a,b) * int(t^(a-1)*(1-t)^(b-1),t=0..x) for real a &gt; 0, b &gt; 0, 1 &gt;= x &gt;= 0.
        /// </summary>
        /// <param name="a">The first Beta parameter, a positive real number.</param>
        /// <param name="b">The second Beta parameter, a positive real number.</param>
        /// <param name="x">The upper limit of the integral.</param>
        /// <returns>The regularized lower incomplete beta function.</returns>
        public static double BetaRegularized(double a, double b, double x)
        {
            if (a < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(a) /*, Resources.ArgumentNotNegative*/);
            }

            if (b < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(b) /*, Resources.ArgumentNotNegative*/);
            }

            if (x < 0.0 || x > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(x) /*, Resources.ArgumentInIntervalXYInclusive*/);
            }

            var bt = (x == 0.0 || x == 1.0)
                ? 0.0
                : Math.Exp(GammaLn(a + b) - GammaLn(a) - GammaLn(b) + (a * Math.Log(x)) + (b * Math.Log(1.0 - x)));

            var symmetryTransformation = x >= (a + 1.0) / (a + b + 2.0);

            /* Continued fraction representation */
            var eps = Precision.DoublePrecision;
            var fpmin = 0.0.Increment() / eps;

            if (symmetryTransformation)
            {
                x = 1.0 - x;
                var swap = a;
                a = b;
                b = swap;
            }

            var qab = a + b;
            var qap = a + 1.0;
            var qam = a - 1.0;
            var c = 1.0;
            var d = 1.0 - (qab * x / qap);

            if (Math.Abs(d) < fpmin)
            {
                d = fpmin;
            }

            d = 1.0 / d;
            var h = d;

            for (int m = 1, m2 = 2; m <= 140; m++, m2 += 2)
            {
                var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
                d = 1.0 + (aa * d);

                if (Math.Abs(d) < fpmin)
                {
                    d = fpmin;
                }

                c = 1.0 + (aa / c);
                if (Math.Abs(c) < fpmin)
                {
                    c = fpmin;
                }

                d = 1.0 / d;
                h *= d * c;
                aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
                d = 1.0 + (aa * d);

                if (Math.Abs(d) < fpmin)
                {
                    d = fpmin;
                }

                c = 1.0 + (aa / c);

                if (Math.Abs(c) < fpmin)
                {
                    c = fpmin;
                }

                d = 1.0 / d;
                var del = d * c;
                h *= del;

                if (Math.Abs(del - 1.0) <= eps)
                {
                    return symmetryTransformation ? 1.0 - (bt * h / a) : bt * h / a;
                }
            }

            return symmetryTransformation ? 1.0 - (bt * h / a) : bt * h / a;
        }

        /// <summary>
        /// Increments a floating point number to the next bigger number representable by the data type.
        /// </summary>
        /// <param name="value">The value which needs to be incremented.</param>
        /// <param name="count">How many times the number should be incremented.</param>
        /// <remarks>
        /// The incrementation step length depends on the provided value.
        /// Increment(double.MaxValue) will return positive infinity.
        /// </remarks>
        /// <returns>The next larger floating point value.</returns>
        public static double Increment(this double value, int count = 1)
        {
            if (double.IsInfinity(value) || double.IsNaN(value) || count == 0)
            {
                return value;
            }

            if (count < 0)
            {
                return Decrement(value, -count);
            }

            // Translate the bit pattern of the double to an integer.
            // Note that this leads to:
            // double > 0 --> long > 0, growing as the double value grows
            // double < 0 --> long < 0, increasing in absolute magnitude as the double
            //                          gets closer to zero!
            //                          i.e. 0 - double.epsilon will give the largest long value!
            long intValue = BitConverter.DoubleToInt64Bits(value);
            if (intValue < 0)
            {
                intValue -= count;
            }
            else
            {
                intValue += count;
            }

            // Note that long.MinValue has the same bit pattern as -0.0.
            if (intValue == long.MinValue)
            {
                return 0;
            }

            // Note that not all long values can be translated into double values. There's a whole bunch of them
            // which return weird values like infinity and NaN
            return BitConverter.Int64BitsToDouble(intValue);
        }

        /// <summary>
        /// Decrements a floating point number to the next smaller number representable by the data type.
        /// </summary>
        /// <param name="value">The value which should be decremented.</param>
        /// <param name="count">How many times the number should be decremented.</param>
        /// <remarks>
        /// The decrementation step length depends on the provided value.
        /// Decrement(double.MinValue) will return negative infinity.
        /// </remarks>
        /// <returns>The next smaller floating point value.</returns>
        public static double Decrement(this double value, int count = 1)
        {
            if (double.IsInfinity(value) || double.IsNaN(value) || count == 0)
            {
                return value;
            }

            if (count < 0)
            {
                return Decrement(value, -count);
            }

            // Translate the bit pattern of the double to an integer.
            // Note that this leads to:
            // double > 0 --> long > 0, growing as the double value grows
            // double < 0 --> long < 0, increasing in absolute magnitude as the double
            //                          gets closer to zero!
            //                          i.e. 0 - double.epsilon will give the largest long value!
            long intValue = BitConverter.DoubleToInt64Bits(value);

            // If the value is zero then we'd really like the value to be -0. So we'll make it -0
            // and then everything else should work out.
            if (intValue == 0)
            {
                // Note that long.MinValue has the same bit pattern as -0.0.
                intValue = long.MinValue;
            }

            if (intValue < 0)
            {
                intValue += count;
            }
            else
            {
                intValue -= count;
            }

            // Note that not all long values can be translated into double values. There's a whole bunch of them
            // which return weird values like infinity and NaN
            return BitConverter.Int64BitsToDouble(intValue);
        }
    }
}