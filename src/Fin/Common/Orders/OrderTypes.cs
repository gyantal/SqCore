namespace QuantConnect.Orders
{
    /// <summary>
    /// Type of the order: market, limit or stop
    /// </summary>
    public enum OrderType
    {
        /// <summary>
        /// Market Order Type (0)
        /// </summary>
        Market,

        /// <summary>
        /// Limit Order Type (1)
        /// </summary>
        Limit,

        /// <summary>
        /// Stop Market Order Type - Fill at market price when break target price (2)
        /// </summary>
        StopMarket,

        /// <summary>
        /// Stop limit order type - trigger fill once pass the stop price; but limit fill to limit price (3)
        /// </summary>
        StopLimit,

        /// <summary>
        /// Market on open type - executed on exchange open (4)
        /// </summary>
        MarketOnOpen,

        /// <summary>
        /// Market on close type - executed on exchange close (5)
        /// </summary>
        MarketOnClose,

        /// <summary>
        /// Option Exercise Order Type (6)
        /// </summary>
        OptionExercise,
        
        /// <summary>
        ///  Limit if Touched Order Type - a limit order to be placed after first reaching a trigger value (7)
        /// </summary>
        LimitIfTouched
    }

    /// <summary>
    /// Direction of the order
    /// </summary>
    public enum OrderDirection
    {
        /// <summary>
        /// Buy Order (0)
        /// </summary>
        Buy,

        /// <summary>
        /// Sell Order (1)
        /// </summary>
        Sell,

        /// <summary>
        /// Default Value - No Order Direction (2)
        /// </summary>
        /// <remarks>
        /// Unfortunately this does not have a value of zero because
        /// there are backtests saved that reference the values in this order
        /// </remarks>
        Hold
    }

    /// <summary>
    /// Fill status of the order class.
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// New order pre-submission to the order processor (0)
        /// </summary>
        New = 0,

        /// <summary>
        /// Order submitted to the market (1)
        /// </summary>
        Submitted = 1,

        /// <summary>
        /// Partially filled, In Market Order (2)
        /// </summary>
        PartiallyFilled = 2,

        /// <summary>
        /// Completed, Filled, In Market Order (3)
        /// </summary>
        Filled = 3,

        /// <summary>
        /// Order cancelled before it was filled (5)
        /// </summary>
        Canceled = 5,

        /// <summary>
        /// No Order State Yet (6)
        /// </summary>
        None = 6,

        /// <summary>
        /// Order invalidated before it hit the market (e.g. insufficient capital) (7)
        /// </summary>
        Invalid = 7,

        /// <summary>
        /// Order waiting for confirmation of cancellation (6)
        /// </summary>
        CancelPending = 8,

        /// <summary>
        /// Order update submitted to the market (9)
        /// </summary>
        UpdateSubmitted = 9
    }
}