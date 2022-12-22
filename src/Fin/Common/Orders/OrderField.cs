namespace QuantConnect.Orders
{
    /// <summary>
    /// Specifies an order field that does not apply to all order types
    /// </summary>
    public enum OrderField
    {
        /// <summary>
        /// The limit price for a <see cref="LimitOrder"/>, <see cref="StopLimitOrder"/> or <see cref="LimitIfTouchedOrder"/> (0)
        /// </summary>
        LimitPrice,

        /// <summary>
        /// The stop price for a <see cref="StopMarketOrder"/> or a <see cref="StopLimitOrder"/> (1)
        /// </summary>
        StopPrice,
        
        /// <summary>
        /// The trigger price for a <see cref="LimitIfTouchedOrder"/> (2)
        /// </summary>
        TriggerPrice
    }
}
