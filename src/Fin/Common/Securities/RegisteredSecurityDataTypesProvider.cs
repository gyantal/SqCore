using System;
using System.Collections.Generic;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides an implementation of <see cref="IRegisteredSecurityDataTypesProvider"/> that permits the
    /// consumer to modify the expected types
    /// </summary>
    public class RegisteredSecurityDataTypesProvider : IRegisteredSecurityDataTypesProvider
    {
        /// <summary>
        /// Provides a reference to an instance of <see cref="IRegisteredSecurityDataTypesProvider"/> that contains no registered types
        /// </summary>
        public static readonly IRegisteredSecurityDataTypesProvider Null = new RegisteredSecurityDataTypesProvider();

        private readonly Dictionary<string, Type> _types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers the specified type w/ the provider
        /// </summary>
        /// <returns>True if the type was previously not registered</returns>
        public bool RegisterType(Type type)
        {
            lock (_types)
            {
                Type existingType;
                if (_types.TryGetValue(type.Name, out existingType))
                {
                    if (existingType != type)
                    {
                        // shouldn't happen but we want to know if it does
                        throw new InvalidOperationException(
                            $"Two different types were detected trying to register the same type name: {existingType} - {type}");
                    }
                    return true;
                }

                _types[type.Name] = type;
                return false;
            }
        }

        /// <summary>
        /// Removes the registration for the specified type
        /// </summary>
        /// <returns>True if the type was previously registered</returns>
        public bool UnregisterType(Type type)
        {
            lock (_types)
            {
                return _types.Remove(type.Name);
            }
        }

        /// <summary>
        /// Gets an enumerable of data types expected to be contained in a <see cref="DynamicSecurityData"/> instance
        /// </summary>
        public bool TryGetType(string name, out Type type)
        {
            lock (_types)
            {
                return _types.TryGetValue(name, out type);
            }
        }
    }
}
