using System;
using System.Collections.Generic;
using System.Threading;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Interface used to handle items being processed and communicate busy state
    /// </summary>
    /// <typeparam name="T">The item type being processed</typeparam>
    public interface IBusyCollection<T> : IDisposable
    {
        /// <summary>
        /// Gets a wait handle that can be used to wait until this instance is done
        /// processing all of it's item
        /// </summary>
        WaitHandle WaitHandle { get; }

        /// <summary>
        /// Gets the number of items held within this collection
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns true if processing, false otherwise
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Adds the items to this collection
        /// </summary>
        /// <param name="item">The item to be added</param>
        void Add(T item);

        /// <summary>
        /// Adds the items to this collection
        /// </summary>
        /// <param name="item">The item to be added</param>
        /// <param name="cancellationToken">A cancellation token to observer</param>
        void Add(T item, CancellationToken cancellationToken);

        /// <summary>
        /// Marks the collection as not accepting any more additions
        /// </summary>
        void CompleteAdding();

        /// <summary>
        /// Provides a consuming enumerable for items in this collection.
        /// </summary>
        /// <returns>An enumerable that removes and returns items from the collection</returns>
        IEnumerable<T> GetConsumingEnumerable();

        /// <summary>
        /// Provides a consuming enumerable for items in this collection.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observer</param>
        /// <returns>An enumerable that removes and returns items from the collection</returns>
        IEnumerable<T> GetConsumingEnumerable(CancellationToken cancellationToken);
    }
}
