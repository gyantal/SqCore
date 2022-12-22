namespace QuantConnect.Indicators
{
    /// <summary>
    /// Event handler type for the IndicatorBase.Updated event
    /// </summary>
    /// <param name="sender">The indicator that fired the event</param>
    /// <param name="updated">The new piece of data produced by the indicator</param>
    public delegate void IndicatorUpdatedHandler(object sender, IndicatorDataPoint updated);
}