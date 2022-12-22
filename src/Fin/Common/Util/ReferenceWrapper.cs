namespace QuantConnect.Util
{
    /// <summary>
    /// We wrap a T instance, a value type, with a class, a reference type, to achieve thread safety when assigning new values
    /// and reading from multiple threads. This is possible because assignments are atomic operations in C# for reference types (among others).
    /// </summary>
    /// <remarks>This is a simpler, performance oriented version of <see cref="Ref"/></remarks>
    public class ReferenceWrapper<T> 
        where T : struct
    {
        /// <summary>
        /// The current value
        /// </summary>
        public readonly T Value;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="value">The value to use</param>
        public ReferenceWrapper(T value)
        {
            Value = value;
        }
    }
}
