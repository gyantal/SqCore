using System;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Represents the brokerage factory type required to load a data queue handler
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class BrokerageFactoryAttribute : Attribute
    {
        /// <summary>
        /// The type of the brokerage factory
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Creates a new instance of the <see cref="BrokerageFactoryAttribute"/> class
        /// </summary>
        /// <param name="type">The brokerage factory type</param>
        public BrokerageFactoryAttribute(Type type)
        {
            Type = type;
        }
    }
}
