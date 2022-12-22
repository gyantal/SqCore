namespace QuantConnect.Algorithm.Framework.Portfolio
{
    /// <summary>
    /// Specifies the bias of the portfolio (Short, Long/Short, Long) 
    /// </summary>
    public enum PortfolioBias
    {
        /// <summary>
        /// Portfolio can only have short positions (-1)
        /// </summary>
        Short = -1,

        /// <summary>
        /// Portfolio can have both long and short positions (0)
        /// </summary>
        LongShort = 0,

        /// <summary>
        /// Portfolio can only have long positions (1)
        /// </summary>
        Long = 1
    }
}