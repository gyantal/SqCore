namespace QuantConnect.Data.Market
{
    /// <summary>
    /// The type of the RenkoBar being created.
    /// Used by RenkoConsolidator and ClassicRenkoConsolidator
    /// </summary>
    /// <remarks>Classic implementation was not entirely accurate for Renko consolidator
    /// so we have replaced it with a new implementation and maintain the classic
    /// for backwards compatibility and comparison.</remarks>
    public enum RenkoType
    {
        /// <summary>
        /// Indicates that the RenkoConsolidator works in its
        /// original implementation; Specifically:
        /// - It only returns a single bar, at most, irrespective of tick movement
        /// - It will emit consecutive bars side by side
        /// - By default even bars are created 
        /// (0)
        /// </summary>
        /// <remarks>the Classic mode has only been retained for
        /// backwards compatibility with existing code.</remarks>
        Classic,

        /// <summary>
        /// Indicates that the RenkoConsolidator works properly;
        /// Specifically:
        /// - returns zero or more bars per tick, as appropriate.
        /// - Will not emit consecutive bars side by side
        /// - Creates 
        /// (1)
        /// </summary>
        Wicked
    }
}
