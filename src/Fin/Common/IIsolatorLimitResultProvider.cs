using System;

namespace QuantConnect
{
    /// <summary>
    /// Provides an abstraction for managing isolator limit results.
    /// This is originally intended to be used by the training feature to permit a single
    /// algorithm time loop to extend past the default of ten minutes
    /// </summary>
    public interface IIsolatorLimitResultProvider
    {
        /// <summary>
        /// Determines whether or not a custom isolator limit has be reached.
        /// </summary>
        IsolatorLimitResult IsWithinLimit();

        /// <summary>
        /// Requests additional time from the isolator result provider. This is intended
        /// to prevent <see cref="IsWithinLimit"/> from returning an error result.
        /// This method will throw a <see cref="TimeoutException"/> if there is insufficient
        /// resources available to fulfill the specified number of minutes.
        /// </summary>
        /// <param name="minutes">The number of additional minutes to request</param>
        void RequestAdditionalTime(int minutes);

        /// <summary>
        /// Attempts to request additional time from the isolator result provider. This is intended
        /// to prevent <see cref="IsWithinLimit"/> from returning an error result.
        /// This method will only return false if there is insufficient resources available to fulfill
        /// the specified number of minutes.
        /// </summary>
        /// <param name="minutes">The number of additional minutes to request</param>
        bool TryRequestAdditionalTime(int minutes);
    }
}