namespace QuantConnect.Orders
{
    /// <summary>
    /// Specifies the type of <see cref="OrderRequest"/>
    /// </summary>
    public enum OrderRequestType
    {
        /// <summary>
        /// The request is a <see cref="SubmitOrderRequest"/> (0)
        /// </summary>
        Submit,

        /// <summary>
        /// The request is a <see cref="UpdateOrderRequest"/> (1)
        /// </summary>
        Update,

        /// <summary>
        /// The request is a <see cref="CancelOrderRequest"/> (2)
        /// </summary>
        Cancel
    }
}