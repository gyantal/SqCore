namespace QuantConnect.Data
{
    /// <summary>
    /// Specifies the format of data in a subscription
    /// </summary>
    public enum FileFormat
    {
        /// <summary>
        /// Comma separated values (0)
        /// </summary>
        Csv,

        /// <summary>
        /// Binary file data (1)
        /// </summary>
        Binary,

        /// <summary>
        /// Only the zip entry names are read in as symbols (2)
        /// </summary>
        ZipEntryName,

        /// <summary>
        /// Reader returns a BaseDataCollection object (3)
        /// </summary>
        /// <remarks>Lean will unfold the collection and consume it as individual data points</remarks>
        UnfoldingCollection,

        /// <summary>
        /// Data stored using an intermediate index source (4)
        /// </summary>
        Index,

        /// <summary>
        /// Data type inherits from BaseDataCollection.
        /// Reader method can return a non BaseDataCollection type which will be folded, based on unique time,
        /// into an instance of the data type (5)
        /// </summary>
        FoldingCollection
    }
}
