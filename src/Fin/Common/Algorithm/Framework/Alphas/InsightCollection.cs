﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Provides a collection for managing insights. This type provides collection access semantics
    /// as well as dictionary access semantics through TryGetValue, ContainsKey, and this[symbol]
    /// </summary>
    public class InsightCollection : ICollection<Insight>
    {
        // for performance lets keep the next insight expiration time
        private DateTime? _nextExpiryTime;
        private readonly ConcurrentDictionary<Symbol, List<Insight>> _insights = new ConcurrentDictionary<Symbol, List<Insight>>();

        /// <summary>Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <returns>The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
        public int Count => _insights.Aggregate(0, (i, kvp) => i + kvp.Value.Count);

        /// <summary>Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</summary>
        /// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.</returns>
        public bool IsReadOnly => false;

        /// <summary>Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public void Add(Insight item)
        {
            _nextExpiryTime = null;
            _insights.AddOrUpdate(item.Symbol, s => new List<Insight> {item}, (s, list) =>
            {
                list.Add(item);
                return list;
            });
        }

        /// <summary>
        /// Adds each item in the specified enumerable of insights to this collection
        /// </summary>
        /// <param name="insights">The insights to add to this collection</param>
        public void AddRange(IEnumerable<Insight> insights)
        {
            foreach (var insight in insights)
            {
                Add(insight);
            }
        }

        /// <summary>Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
        public void Clear()
        {
            _nextExpiryTime = null;
            _insights.Clear();
        }

        /// <summary>Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.</summary>
        /// <returns>true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false.</returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        public bool Contains(Insight item)
        {
            List<Insight> symbolInsights;
            if (_insights.TryGetValue(item.Symbol, out symbolInsights))
            {
                return symbolInsights.Contains(item);
            }

            return false;
        }

        /// <summary>
        /// Determines whether insights exist in this collection for the specified symbol
        /// </summary>
        /// <param name="symbol">The symbol key</param>
        /// <returns>True if there are insights for the symbol in this collection</returns>
        public bool ContainsKey(Symbol symbol)
        {
            List<Insight> insights;
            return _insights.TryGetValue(symbol, out insights) && insights.Count > 0;
        }

        /// <summary>Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.</summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="array" /> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex" /> is less than 0.</exception>
        /// <exception cref="T:System.ArgumentException">The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1" /> is greater than the available space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.</exception>
        public void CopyTo(Insight[] array, int arrayIndex)
        {
            // Avoid calling `ToList` on insights to avoid potential infinite loop (issue #3168)
            Array.Copy(_insights.SelectMany(kvp => kvp.Value).ToArray(), 0, array, arrayIndex, Count);
        }

        /// <summary>Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.</summary>
        /// <returns>true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
        public bool Remove(Insight item)
        {
            List<Insight> symbolInsights;
            if (_insights.TryGetValue(item.Symbol, out symbolInsights))
            {
                _nextExpiryTime = null;

                if (symbolInsights.Remove(item))
                {
                    // remove empty list from dictionary
                    if (symbolInsights.Count == 0)
                    {
                        _insights.TryRemove(item.Symbol, out symbolInsights);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Dictionary accessor returns a list of insights for the specified symbol
        /// </summary>
        /// <param name="symbol">The symbol key</param>
        /// <returns>List of insights for the symbol</returns>
        public List<Insight> this[Symbol symbol]
        {
            get { return _insights[symbol]; }
            set { _insights[symbol] = value; }
        }

        /// <summary>
        /// Attempts to get the list of insights with the specified symbol key
        /// </summary>
        /// <param name="symbol">The symbol key</param>
        /// <param name="insights">The insights for the specified symbol, or null if not found</param>
        /// <returns>True if insights for the specified symbol were found, false otherwise</returns>
        public bool TryGetValue(Symbol symbol, out List<Insight> insights)
        {
            return _insights.TryGetValue(symbol, out insights);
        }

        /// <summary>Returns an enumerator that iterates through the collection.</summary>
        /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.</returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<Insight> GetEnumerator()
        {
            return _insights.SelectMany(kvp => kvp.Value).GetEnumerator();
        }

        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Removes the symbol and its insights
        /// </summary>
        /// <param name="symbols">List of symbols that will be removed</param>
        public void Clear(Symbol[] symbols)
        {
            _nextExpiryTime = null;
            foreach (var symbol in symbols)
            {
                List<Insight> insights;
                _insights.TryRemove(symbol, out insights);
            }
        }

        /// <summary>
        /// Gets the next expiry time UTC
        /// </summary>
        public DateTime? GetNextExpiryTime()
        {
            if (Count == 0)
            {
                return null;
            }

            if (_nextExpiryTime != null)
            {
                return _nextExpiryTime;
            }

            _nextExpiryTime = _insights.Min(x => x.Value.Min(i => i.CloseTimeUtc));
            return _nextExpiryTime;
        }

        /// <summary>
        /// Gets the last generated active insight
        /// </summary>
        /// <returns>Collection of insights that are active</returns>
        public ICollection<Insight> GetActiveInsights(DateTime utcTime)
        {
            var activeInsights = new List<Insight>();
            foreach (var kvp in _insights)
            {
                foreach (var insight in kvp.Value)
                {
                    if (insight.IsActive(utcTime))
                    {
                        activeInsights.Add(insight);
                    }
                }
            }
            return activeInsights;
        }

        /// <summary>
        /// Returns true if there are active insights for a given symbol and time
        /// </summary>
        /// <param name="symbol">The symbol key</param>
        /// <param name="utcTime">Time that determines whether the insight has expired</param>
        /// <returns></returns>
        public bool HasActiveInsights(Symbol symbol, DateTime utcTime)
        {
            List<Insight> insights;
            if (TryGetValue(symbol, out insights))
            {
                return insights.Any(i => i.IsActive(utcTime));
            }
            return false;
        }

        /// <summary>
        /// Remove all expired insights from the collection and retuns them
        /// </summary>
        /// <param name="utcTime">Time that determines whether the insight has expired</param>
        /// <returns>Expired insights that were removed</returns>
        public ICollection<Insight> RemoveExpiredInsights(DateTime utcTime)
        {
            var removedInsights = new List<Insight>();
            foreach (var kvp in _insights)
            {
                foreach (var insight in kvp.Value)
                {
                    if (insight.IsExpired(utcTime))
                    {
                        removedInsights.Add(insight);
                    }
                }
            }
            foreach (var insight in removedInsights)
            {
                Remove(insight);
            }
            return removedInsights;
        }
    }
}