using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// An implementation of <see cref="IEnumerator{T}"/> that relies on the
    /// <see cref="Enqueue"/> method being called and only ends when <see cref="Stop"/>
    /// is called
    /// </summary>
    /// <typeparam name="T">The item type yielded by the enumerator</typeparam>
    public class EnqueueableEnumerator<T> : IEnumerator<T>
    {
        private T _current;
        private T _lastEnqueued;
        private volatile bool _end;
        private volatile bool _disposed;

        private readonly bool _isBlocking;
        private readonly int _timeout;
        private readonly object _lock = new object();
        private readonly BlockingCollection<T> _blockingCollection;

        /// <summary>
        /// Gets the current number of items held in the internal queue
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    if (_end) return 0;
                    return _blockingCollection.Count;
                }
            }
        }

        /// <summary>
        /// Gets the last item that was enqueued
        /// </summary>
        public T LastEnqueued
        {
            get { return _lastEnqueued; }
        }

        /// <summary>
        /// Returns true if the enumerator has finished and will not accept any more data
        /// </summary>
        public bool HasFinished
        {
            get { return _end || _disposed; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnqueueableEnumerator{T}"/> class
        /// </summary>
        /// <param name="blocking">Specifies whether or not to use the blocking behavior</param>
        public EnqueueableEnumerator(bool blocking = false)
        {
            _blockingCollection = new BlockingCollection<T>();
            _isBlocking = blocking;
            _timeout = blocking ? Timeout.Infinite : 0;
        }

        /// <summary>
        /// Enqueues the new data into this enumerator
        /// </summary>
        /// <param name="data">The data to be enqueued</param>
        public void Enqueue(T data)
        {
            lock (_lock)
            {
                if (_end) return;
                _blockingCollection.Add(data);
                _lastEnqueued = data;
            }
        }

        /// <summary>
        /// Signals the enumerator to stop enumerating when the items currently
        /// held inside are gone. No more items will be added to this enumerator.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_end) return;
                // no more items can be added, so no need to wait anymore
                _blockingCollection.CompleteAdding();
                _end = true;
            }
        }

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        /// <returns>
        /// true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.
        /// </returns>
        /// <exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created. </exception><filterpriority>2</filterpriority>
        public bool MoveNext()
        {
            T current;
            if (!_blockingCollection.TryTake(out current, _timeout))
            {
                _current = default(T);

                // if the enumerator has blocking behavior and there is no more data, it has ended
                if (_isBlocking)
                {
                    lock (_lock)
                    {
                        _end = true;
                    }
                }

                return !_end;
            }

            _current = current;

            // even if we don't have data to return, we haven't technically
            // passed the end of the collection, so always return true until
            // the enumerator is explicitly disposed or ended
            return true;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the collection.
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created. </exception><filterpriority>2</filterpriority>
        public void Reset()
        {
            throw new NotImplementedException("EnqueableEnumerator.Reset() has not been implemented yet.");
        }

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        /// <returns>
        /// The element in the collection at the current position of the enumerator.
        /// </returns>
        public T Current
        {
            get { return _current; }
        }

        /// <summary>
        /// Gets the current element in the collection.
        /// </summary>
        /// <returns>
        /// The current element in the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        object IEnumerator.Current
        {
            get { return Current; }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                Stop();
                if (_blockingCollection != null) _blockingCollection.Dispose();
                _disposed = true;
            }
        }
    }
}