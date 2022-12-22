namespace QuantConnect.Indicators
{
    /// <summary>
    /// Defines the different types of moving averages
    /// </summary>  
    public enum MovingAverageType
    {
        /// <summary>
        /// An unweighted, arithmetic mean (0)
        /// </summary>
        Simple,
        /// <summary>
        /// The standard exponential moving average, using a smoothing factor of 2/(n+1) (1)
        /// </summary>
        Exponential,
        /// <summary>
        /// An exponential moving average, using a smoothing factor of 1/n and simple moving average as seeding (2)
        /// </summary>
        Wilders,
        /// <summary>
        /// A weighted moving average type (3)
        /// </summary>
        LinearWeightedMovingAverage,
        /// <summary>
        /// The double exponential moving average (4)
        /// </summary>
        DoubleExponential,
        /// <summary>
        /// The triple exponential moving average (5)
        /// </summary>
        TripleExponential,
        /// <summary>
        /// The triangular moving average (6)
        /// </summary>
        Triangular,
        /// <summary>
        /// The T3 moving average (7)
        /// </summary>
        T3,
        /// <summary>
        /// The Kaufman Adaptive Moving Average (8)
        /// </summary>
        Kama,
        /// <summary>
        /// The Hull Moving Average (9)
        /// </summary>
        Hull,
        /// <summary>
        /// The Arnaud Legoux Moving Average (10)
        /// </summary>
        Alma
    }
}
