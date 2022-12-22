using System;

namespace QuantConnect
{
    /// <summary>
    /// Represents the result of the <see cref="Isolator"/> limiter callback
    /// </summary>
    public class IsolatorLimitResult
    {
        /// <summary>
        /// Gets the amount of time spent on the current time step
        /// </summary>
        public TimeSpan CurrentTimeStepElapsed { get; }

        /// <summary>
        /// Gets the error message or an empty string if no error on the current time step
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Returns true if there are no errors in the current time step
        /// </summary>
        public bool IsWithinCustomLimits => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Initializes a new instance of the <see cref="IsolatorLimitResult"/> class
        /// </summary>
        /// <param name="currentTimeStepElapsed">The amount of time spent on the current time step</param>
        /// <param name="errorMessage">The error message or an empty string if no error on the current time step</param>
        public IsolatorLimitResult(TimeSpan currentTimeStepElapsed, string errorMessage)
        {
            ErrorMessage = errorMessage;
            CurrentTimeStepElapsed = currentTimeStepElapsed;
        }
    }
}
