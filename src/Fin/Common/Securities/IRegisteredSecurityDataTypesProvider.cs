using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides the set of base data types registered in the algorithm
    /// </summary>
    public interface IRegisteredSecurityDataTypesProvider
    {
        /// <summary>
        /// Registers the specified type w/ the provider
        /// </summary>
        /// <returns>True if the type was previously not registered</returns>
        bool RegisterType(Type type);

        /// <summary>
        /// Removes the registration for the specified type
        /// </summary>
        /// <returns>True if the type was previously registered</returns>
        bool UnregisterType(Type type);

        /// <summary>
        /// Determines if the specified type is registered or not and returns it
        /// </summary>
        /// <returns>True if the type was previously registered</returns>
        bool TryGetType(string name, out Type type);
    }
}