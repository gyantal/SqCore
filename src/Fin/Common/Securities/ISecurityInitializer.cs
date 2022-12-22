namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a type capable of initializing a new security
    /// </summary>
    public interface ISecurityInitializer
    {
        /// <summary>
        /// Initializes the specified security
        /// </summary>
        /// <param name="security">The security to be initialized</param>
        void Initialize(Security security);
    }

    /// <summary>
    /// Provides static access to the <see cref="Null"/> security initializer
    /// </summary>
    public static class SecurityInitializer
    {
        /// <summary>
        /// Gets an implementation of <see cref="ISecurityInitializer"/> that is a no-op
        /// </summary>
        public static readonly ISecurityInitializer Null = new NullSecurityInitializer();

        private sealed class NullSecurityInitializer : ISecurityInitializer
        {
            public void Initialize(Security security) { }
        }
    }
}