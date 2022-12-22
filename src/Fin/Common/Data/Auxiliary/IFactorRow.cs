using System;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Factor row abstraction. <see cref="IFactorProvider"/>
    /// </summary>
    public interface IFactorRow
    {
        /// <summary>
        /// Gets the date associated with this data
        /// </summary>
        DateTime Date { get; }

        /// <summary>
        /// Writes factor file row into it's file format
        /// </summary>
        string GetFileFormat(string source = null);
    }
}
