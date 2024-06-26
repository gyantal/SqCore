using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using QuantConnect.Util;

namespace QuantConnect.Data
{
    /// <summary>
    /// Dynamic Data Class: Accept flexible data, adapting to the columns provided by source.
    /// </summary>
    /// <remarks>Intended for use with Quandl class.</remarks>
    public abstract class DynamicData : BaseData, IDynamicMetaObjectProvider
    {
        private static readonly MethodInfo SetPropertyMethodInfo = typeof(DynamicData).GetMethod("SetProperty");
        private static readonly MethodInfo GetPropertyMethodInfo = typeof(DynamicData).GetMethod("GetProperty");

        private readonly IDictionary<string, object> _storage = new Dictionary<string, object>();

        /// <summary>
        /// Get the metaObject required for Dynamism.
        /// </summary>
        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            return new GetSetPropertyDynamicMetaObject(parameter, this, SetPropertyMethodInfo, GetPropertyMethodInfo);
        }

        /// <summary>
        /// Sets the property with the specified name to the value. This is a case-insensitve search.
        /// </summary>
        /// <param name="name">The property name to set</param>
        /// <param name="value">The new property value</param>
        /// <returns>Returns the input value back to the caller</returns>
        public object SetProperty(string name, object value)
        {
            name = name.ToLowerInvariant();

            if (name == "time")
            {
                Time = (DateTime)value;
            }
            if (name == "endtime")
            {
                EndTime = (DateTime)value;
            }
            if (name == "value")
            {
                Value = (decimal)value;
            }
            if (name == "symbol")
            {
                if (value is string)
                {
                    Symbol = SymbolCache.GetSymbol((string) value);
                }
                else
                {
                    Symbol = (Symbol) value;
                }
            }
            // reaodnly
            //if (name == "Price")
            //{
            //    return Price = (decimal) value;
            //}
            _storage[name] = value;
            return value;
        }

        /// <summary>
        /// Gets the property's value with the specified name. This is a case-insensitve search.
        /// </summary>
        /// <param name="name">The property name to access</param>
        /// <returns>object value of BaseData</returns>
        public object GetProperty(string name)
        {
            name = name.ToLowerInvariant();

            // redirect these calls to the base types properties
            if (name == "time")
            {
                return Time;
            }
            if (name == "endtime")
            {
                return EndTime;
            }
            if (name == "value")
            {
                return Value;
            }
            if (name == "symbol")
            {
                return Symbol;
            }
            if (name == "price")
            {
                return Price;
            }

            object value;
            if (!_storage.TryGetValue(name, out value))
            {
                // let the user know the property name that we couldn't find
                throw new KeyNotFoundException(
                    $"Property with name \'{name}\' does not exist. Properties: Time, Symbol, Value {string.Join(", ", _storage.Keys)}"
                );
            }

            return value;
        }

        /// <summary>
        /// Gets whether or not this dynamic data instance has a property with the specified name.
        /// This is a case-insensitve search.
        /// </summary>
        /// <param name="name">The property name to check for</param>
        /// <returns>True if the property exists, false otherwise</returns>
        public bool HasProperty(string name)
        {
            return _storage.ContainsKey(name.ToLowerInvariant());
        }

        /// <summary>
        /// Gets the storage dictionary
        /// Python algorithms need this information since DynamicMetaObject does not work
        /// </summary>
        /// <returns>Dictionary that stores the paramenters names and values</returns>
        public IDictionary<string, object> GetStorageDictionary()
        {
            return _storage;
        }

        /// <summary>
        /// Return a new instance clone of this object, used in fill forward
        /// </summary>
        /// <remarks>
        /// This base implementation uses reflection to copy all public fields and properties
        /// </remarks>
        /// <returns>A clone of the current object</returns>
        public override BaseData Clone()
        {
            var clone = ObjectActivator.Clone(this);
            foreach (var kvp in _storage)
            {
                // don't forget to add the dynamic members!
                clone._storage.Add(kvp);
            }
            return clone;
        }
    }
}
