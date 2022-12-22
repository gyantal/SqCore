using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides a functional implementation of <see cref="ISecurityInitializer"/>
    /// </summary>
    public class FuncSecurityInitializer : ISecurityInitializer
    {
        private readonly Action<Security> _initializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuncSecurityInitializer"/> class
        /// </summary>
        /// <param name="initializer">The functional implementation of <see cref="ISecurityInitializer.Initialize"/></param>
        public FuncSecurityInitializer(Action<Security> initializer)
        {
            _initializer = initializer;
        }

        /// <summary>
        /// Initializes the specified security
        /// </summary>
        /// <param name="security">The security to be initialized</param>
        public void Initialize(Security security)
        {
            _initializer(security);
        }
    }
}
