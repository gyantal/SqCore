namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines a base class for <see cref="Security"/> related events
    /// </summary>
    public abstract class SecurityEventArgs
    {
        /// <summary>
        /// Gets the security related to this event
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityEventArgs"/> class
        /// </summary>
        /// <param name="security">The security</param>
        protected SecurityEventArgs(Security security)
        {
            Security = security;
        }
    }
}
