using QuantConnect.Securities;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Reduced interface which provides an instance which implements <see cref="ISecurityInitializer"/>
    /// </summary>
    public interface ISecurityInitializerProvider
    {
        /// <summary>
        /// Gets an instance that is to be used to initialize newly created securities.
        /// </summary>
        ISecurityInitializer SecurityInitializer
        {
            get;
        }
    }
}
