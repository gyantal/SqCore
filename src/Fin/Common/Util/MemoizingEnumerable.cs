using System.Collections;
using System.Collections.Generic;

namespace QuantConnect.Util
{
    /// <summary>
    /// Defines an enumerable that can be enumerated many times while
    /// only performing a single enumeration of the root enumerable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MemoizingEnumerable<T> : IEnumerable<T>
    {
        private List<T> _buffer;
        private IEnumerator<T> _enumerator;

        /// <summary>
        /// Allow disableing the buffering
        /// </summary>
        /// <remarks>Should be called before the enumeration starts</remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoizingEnumerable{T}"/> class
        /// </summary>
        /// <param name="enumerable">The source enumerable to be memoized</param>
        public MemoizingEnumerable(IEnumerable<T> enumerable)
        {
            Enabled = true;
            _enumerator = enumerable.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<T> GetEnumerator()
        {
            if (!Enabled)
            {
                if (_enumerator != null)
                {
                    while (_enumerator.MoveNext())
                    {
                        yield return _enumerator.Current;
                    }

                    // important to avoid leak!
                    _enumerator.Dispose();
                    _enumerator = null;
                }
            }
            else
            {
                if (_buffer == null)
                {
                    // lazy create our buffer
                    _buffer = new List<T>();
                }

                int i = 0;
                while (i <= _buffer.Count)
                {
                    // sync for multiple threads access to _enumerator and _buffer
                    lock (_buffer)
                    {
                        // check to see if we need to move next
                        if (_enumerator != null && i >= _buffer.Count)
                        {
                            if (_enumerator.MoveNext())
                            {
                                var value = _enumerator.Current;
                                _buffer.Add(value);
                                yield return value;
                            }
                            else
                            {
                                // important to avoid leak!
                                _enumerator.Dispose();
                                _enumerator = null;
                            }
                        }
                        else
                        {
                            // we have a value if it's in the buffer
                            if (_buffer.Count > i)
                            {
                                yield return _buffer[i];
                            }
                        }
                    }

                    // increment for next time
                    i++;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
