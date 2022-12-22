using System;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Events related to data providers
    /// </summary>
    public interface IDataProviderEvents
    {
        /// <summary>
        /// Event fired when an invalid configuration has been detected
        /// </summary>
        event EventHandler<InvalidConfigurationDetectedEventArgs> InvalidConfigurationDetected;

        /// <summary>
        /// Event fired when the numerical precision in the factor file has been limited
        /// </summary>
        event EventHandler<NumericalPrecisionLimitedEventArgs> NumericalPrecisionLimited;

        /// <summary>
        /// Event fired when there was an error downloading a remote file
        /// </summary>
        event EventHandler<DownloadFailedEventArgs> DownloadFailed;

        /// <summary>
        /// Event fired when there was an error reading the data
        /// </summary>
        event EventHandler<ReaderErrorDetectedEventArgs> ReaderErrorDetected;

        /// <summary>
        /// Event fired when the start date has been limited
        /// </summary>
        event EventHandler<StartDateLimitedEventArgs> StartDateLimited;
    }
}
