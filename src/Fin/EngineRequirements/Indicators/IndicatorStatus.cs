namespace QuantConnect.Indicators
{
    /// <summary>
    /// The possible states returned by <see cref="IndicatorBase{T}.ComputeNextValue"/>
    /// </summary>
    public enum IndicatorStatus
    {
        /// <summary>
        /// The indicator successfully calculated a value for the input data (0)
        /// </summary>
        Success,

        /// <summary>
        /// The indicator detected an invalid input data point or tradebar (1)
        /// </summary>
        InvalidInput,

        /// <summary>
        /// The indicator encountered a math error during calculations (2)
        /// </summary>
        MathError,

        /// <summary>
        /// The indicator value is not ready (3)
        /// </summary>
        ValueNotReady
    }
}
