namespace QuantConnect.Securities
{
    /// <summary>
    /// A continuous security that get's mapped during his life
    /// </summary>
    public interface IContinuousSecurity
    {
        /// <summary>
        /// Gets or sets the currently mapped symbol for the security
        /// </summary>
        Symbol Mapped { get; set; }
    }
}
