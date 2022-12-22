using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Util
{
    /// <summary>
    /// An implementation of <see cref="IEqualityComparer{T}"/> for <see cref="List{T}"/>.
    /// Useful when using a <see cref="List{T}"/> as the key of a collection.
    /// </summary>
    /// <typeparam name="T">The list type</typeparam>
    public class ListComparer<T> : IEqualityComparer<List<T>>
    {
        /// <summary>Determines whether the specified objects are equal.</summary>
        /// <returns>true if the specified objects are equal; otherwise, false.</returns>
        public bool Equals(List<T> x, List<T> y)
        {
            return x.SequenceEqual(y);
        }

        /// <summary>Returns a hash code for the specified object.</summary>
        /// <returns>A hash code for the specified object created from combining the hash
        /// code of all the elements in the collection.</returns>
        public int GetHashCode(List<T> obj)
        {
            var hashCode = 0;
            foreach (var dateTime in obj)
            {
                hashCode = (hashCode * 397) ^ dateTime.GetHashCode();
            }
            return hashCode;
        }
    }
}
