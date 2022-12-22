using System;
using System.Collections;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Enumerator that allow applying a filtering function
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FilterEnumerator<T> : IEnumerator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly Func<T, bool> _filter;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="enumerator">The underlying enumerator to filter on</param>
        /// <param name="filter">The filter to apply</param>
        public FilterEnumerator(IEnumerator<T> enumerator, Func<T, bool> filter)
        {
            _enumerator = enumerator;
            _filter = filter;
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            _enumerator.Dispose();
        }

        #endregion

        #region Implementation of IEnumerator

        public bool MoveNext()
        {
            // run the enumerator until it passes the specified filter
            while (_enumerator.MoveNext())
            {
                if (_filter(_enumerator.Current))
                {
                    return true;
                }
            }
            return false;
        }

        public void Reset()
        {
            _enumerator.Reset();
        }

        public T Current
        {
            get { return _enumerator.Current; }
        }

        object IEnumerator.Current
        {
            get { return _enumerator.Current; }
        }

        #endregion
    }
}
