using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// https://github.com/mathnet/mathnet-numerics/blob/8291d7b619310a34cb17f30c428706a295dc11de/src/Numerics/Distributions/StudentT.cs
namespace MathCommon.MathNet;

/// <summary>
/// Continuous Univariate Student's T-distribution.
/// Implements the univariate Student t-distribution. For details about this
/// distribution, see
/// <a href="http://en.wikipedia.org/wiki/Student%27s_t-distribution">
/// Wikipedia - Student's t-distribution</a>.
/// </summary>
/// <remarks><para>We use a slightly generalized version (compared to
/// Wikipedia) of the Student t-distribution. Namely, one which also
/// parameterizes the location and scale. See the book "Bayesian Data
/// Analysis" by Gelman et al. for more details.</para>
/// <para>The density of the Student t-distribution  p(x|mu,scale,dof) =
/// Gamma((dof+1)/2) (1 + (x - mu)^2 / (scale * scale * dof))^(-(dof+1)/2) /
/// (Gamma(dof/2)*Sqrt(dof*pi*scale)).</para>
/// <para>The distribution will use the <see cref="System.Random"/> by
/// default.  Users can get/set the random number generator by using the
/// <see cref="RandomSource"/> property.</para>
/// <para>The statistics classes will check all the incoming parameters
/// whether they are in the allowed range. This might involve heavy
/// computation. Optionally, by setting Control.CheckDistributionParameters
/// to <c>false</c>, all parameter checks can be turned off.</para></remarks>
public class StudentT
{
    // System.Random _random;

    // readonly double _location;
    // readonly double _scale;
    // readonly double _freedom;

    /// <summary>
    /// Initializes a new instance of the StudentT class. This is a Student t-distribution with location 0.0
    /// scale 1.0 and degrees of freedom 1.
    /// </summary>
    public StudentT()
    {
        // _random = SystemRandomSource.Default;
        // _location = 0.0;
        // _scale = 1.0;
        // _freedom = 1.0;
    }

    /// <summary>
    /// Computes the cumulative distribution (CDF) of the distribution at x, i.e. P(X ≤ x).
    /// </summary>
    /// <param name="location">The location (μ) of the distribution.</param>
    /// <param name="scale">The scale (σ) of the distribution. Range: σ > 0.</param>
    /// <param name="freedom">The degrees of freedom (ν) for the distribution. Range: ν > 0.</param>
    /// <param name="x">The location at which to compute the cumulative distribution function.</param>
    /// <returns>the cumulative distribution at location <paramref name="x"/>.</returns>
    /// <seealso cref="CumulativeDistribution"/>
    public static double CDF(double location, double scale, double freedom, double x)
    {
        // for T-value (x) -1.65, it returns p-value = 5.01%        so, for negative T-values, it is OK.
        // for T-value (x) +1.65, it returns p-value = 94.99% (because it is a CDF),       so, for positive T-values, you have to invert it.

        if (scale <= 0.0 || freedom <= 0.0)
        {
            throw new ArgumentException("Argument problem", nameof(scale));
        }

        //// TODO JVG we can probably do a better job for Cauchy special case
        // if (double.IsPositiveInfinity(freedom))
        // {
        //    return Normal.CDF(location, scale, x);
        // }

        var k = (x - location) / scale;
        var h = freedom / (freedom + (k * k));
        var ib = 0.5 * SpecialFunctions.BetaRegularized(freedom / 2.0, 0.5, h);
        return x <= location ? ib : 1.0 - ib;
    }
}