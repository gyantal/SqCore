
// StockID itself doesn't identify a TimeSeries. A time series can be on AAPL, but one can be monthy, weekly, daily, 15min time series. Each belong to the same StockID.
// Learn how others implement the TimeSeries structure on GitHub.

// Do we have to store Date field? Probably yes, because some dates could be missing in the middle
// If Date is stored with ClosePrice, and then Date is stored with OpenPrice, High-LowPrice, it is better to factor out the Date field. 
// So, we have a big struct for each date. That is not exactly a fast Time-series I imagined.

// try to minimize memory footprint for fast backtests and that small memory footprint on Server. AWS RAM is expensive.


// **************** MemDb to QuickTester: What is the fastest access of prices for QuickTester. That wants to get the only ClosePrice data between StartDate and EndDate
// >Time series usage example: 'g_MemDb["MSFT"].Dates'. Dates is naturally increasing. So, it should be an OrderedList, so finding an item is O(LogN), not O(N). Same for indEndDate.
// >DatesToQuickTester = MemArrayCopy(MemDb["MSFT"].Dates) between indStartDate to indEndDate). If Dates is not a standalone array, ArrayCopy would not be possible.
// >ClosePrices givivg To QuickTester: MemArrayCopy(MemDb["MSFT"].ClosePrices) between indStartDate to indEndDate can also work and fast.
// >Quicktester better ArrayCopy and clone this needed historical data, so it can manipulate privately (and there is no multithreading problem if MemDb refreshes it from YahooFinance at 8:00 or when it can.)
// There is no faster way of serving QuickTester from MemDB
// The fastest access is very similar to Dotnet SortedList, but instead of 1 value array, we have separate.
// https://github.com/dotnet/corefx/blob/master/src/System.Collections/src/System/Collections/Generic/SortedList.cs

// This is better smaller RAM storage as well than the alternative simplest List<Date, DailyRecord>, 
// because DailyRecord would contain All potential TickType fields that is EVER used, consuming huge RAM
// Also Date, ClosePrice values are stored in Array, which is much faster than List

// Keep the TKey as parameter as: DateTime (A DateTime is a 8 byte struct, per millisecond data), DateTimeAsInt (4 byte, per minute data), SqDateOnly (2 byte) 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SqCommon;
using System.Runtime.CompilerServices;

namespace FinTechCommon
{
    // https://sourcelens.com.au/Consulting/Brw/Ru/z_dir_zcrez_dir_zsrcz_dir_zcoreclrz_dir_zbinz_dir_zobjz_dir_zWindows_NTq_dot_qx64q_dot_qDebugz_dir_zSystemq_dot_qPrivateq_dot_qCoreLibz_dir_zSRq_dot_qcs
    internal static partial class SR    // auto-generated during the build based on the .resx file
    {

        public static string Format(string format, params object[] args)
        {
            return String.Format(format, args);
        }
        internal static string ArgumentOutOfRange_NeedNonNegNum
        {
            get { return @"Non-negative number required."; }
        }
        internal static string Argument_AddingDuplicate
        {
            get { return @"An item with the same key has already been added."; }
        }

        internal static string ArgumentOutOfRange_SmallCapacity
        {
            get { return @"capacity was less than the current size."; }
        }

        internal static string Arg_WrongType
        {
            get { return @"The value ""{0}"" is not of type ""{1}"" and cannot be used in this generic collection."; }
        }

        internal static string ArgumentOutOfRange_Index
        {
            get { return @"Index was out of range. Must be non-negative and less than the size of the collection."; }
        }

        internal static string Arg_ArrayPlusOffTooSmall
        {
            get { return @"Destination array is not long enough to copy all the items in the collection. Check array index and length."; }
        }

        internal static string Arg_RankMultiDimNotSupported
        {
            get { return @"Only single dimensional arrays are supported for the requested action."; }
        }

        internal static string Arg_NonZeroLowerBound
        {
            get { return @"The lower bound of target array must be zero."; }
        }

        internal static string Argument_InvalidArrayType
        {
            get { return @"Target array type is not compatible with the type of items in the collection."; }
        }

        internal static string InvalidOperation_EnumOpCantHappen
        {
            get { return @"Enumeration has either not started or has already finished."; }
        }

        internal static string InvalidOperation_EnumFailedVersion
        {
            get { return @"Collection was modified; enumeration operation may not execute."; }
        }

        internal static string Arg_KeyNotFoundWithKey
        {
            get { return @"The given key '{0}' was not present in the dictionary."; }
        }

        internal static string NotSupported_KeyCollectionSet
        {
            get { return @"Mutating a key collection derived from a dictionary is not allowed."; }
        }




        internal static string NotSupported_SortedListNestedWrite
        {
            get { return string.Empty; }
        }

    }

    public enum TickType { /* StockQuote */ Open, Close, High, Low, Volume, Dividend, SplitRatio, SplitAdjClose, SplitDivAdjClose, /* Options */ Ask, Bid, Last, OpenInterest, /* Futures */ Settle, EFP, /* Stock other */ SHIR, SharesOutstanding }

    // see SortedList<TKey,TValue> as template https://github.com/dotnet/corefx/blob/master/src/System.Collections/src/System/Collections/Generic/SortedList.cs
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    public class FinTimeSeries<TKey, TValue1, TValue2> : ICollection where TKey : notnull   // Tkey = DateTime (8 byte), DateTimeAsInt (4 byte), SqDateOnly (2 byte), or any int, byte (1 byte), or even string (that can be ordered)
    {
        private TKey[] keys;  // Key which is used for OrderBy the other arrays. This array should be ordered from smallest to largest
        public Dictionary<TickType, TValue1[]> values1;
        public Dictionary<TickType, TValue2[]> values2;
        private int _size; // Do not rename (binary serialization)
        private int version; // Do not rename (binary serialization)
        private readonly IComparer<TKey> comparer; // Do not rename (binary serialization)
        private KeyList? keyList; // Do not rename (binary serialization)
        private Dictionary<TickType, ValueList1?> valueList1; // Do not rename (binary serialization)
        private Dictionary<TickType, ValueList2?> valueList2; // Do not rename (binary serialization)

        private const int DefaultCapacity = 4;

        static void HowToUseThisClassExamples()
        {
            // 1. set up timeSeries
            var ts2 = new FinTimeSeries<SqDateOnly, float, uint>(new TickType[] { TickType.SplitDivAdjClose }, new TickType[] { TickType.Volume });
            ts2.Capacity = 10;  // set capacity will increase all TickType arrays

            // Arrays are refence types. Just create the arrays and pass it to the constructor
            var kvpar1 = new KeyValuePair<TickType, float[]>(TickType.SplitDivAdjClose, Array.Empty<float>());
            var kvpar2 = new KeyValuePair<TickType, uint[]>(TickType.Volume, Array.Empty<uint>());
            var ts1 = new FinTimeSeries<SqDateOnly, float, uint>(
                new SqDateOnly[] { },
                new KeyValuePair<TickType, float[]>[] { kvpar1 },
                new KeyValuePair<TickType, uint[]>[] { kvpar2 }
            );


            // 2. consume timeSeries via public methods
            float ts1YesterdayClose3 = ts1.GetValues1(new SqDateOnly(), TickType.Close);  // neat if data is accessed via methods
            bool isOkTs1YesterdayClose3 = ts1.TryGetValue1(new SqDateOnly(), TickType.Close, out float value);

            // access array via a List, indirectly. There is no memory consumption. Direct reference to the private array.
            // supports only: [] indexer as one by one direct access, and CopyTo() into destination TValue1[] arrays.
            IList<float> array2 = ts1.Values1(TickType.Close);

            // 3. access timeSeries via private members (can be accessed only from inside the class)
            float[] array1 = ts1.values1[TickType.Close];       // access array directly, although it should be private
            float ts1YesterdayClose2 = ts1.values1[TickType.Close][ts1.IndexOfKey(new SqDateOnly())];   // possible to access inner members and manipulate if needed

            // 4. The most efficient, faster usage is the direct usage of the array. Better than (linked-) List, and there is no indirection.
            SqDateOnly[] dates = ts1.GetKeyArrayDirect();
            float[] sdaClose = ts1.GetValue1ArrayDirect(TickType.SplitDivAdjClose);

            // Example usage from MemDb:
            // Security sec = MemDb.gMemDb.GetFirstMatchingSecurity(r.Ticker);
            // SqDateOnly[] dates = sec.DailyHistory.GetKeyArrayDirect();
            // float[] sdaCloses = sec.DailyHistory.GetValue1ArrayDirect(TickType.SplitDivAdjClose);
            // // At 16:00, or even intraday: YF gives even the today last-realtime price with a today-date. We have to find any date backwards, which is NOT today. That is the PreviousClose.
            // int i = (dates[dates.Length - 1] >= new SqDateOnly(DateTime.UtcNow)) ? dates.Length - 2 : dates.Length - 1;
            // Debug.WriteLine($"Found: {r.Ticker}, {dates[i]}:{sdaCloses[i]}");

            // DateTime[] ts1LastWeekDates = ts1.GetRangeIncDate(new DateTime(), new DateTime());
            // double[] ts1LastWeekCloses = ts1.GetRangeIncDouble(new DateTime(), new DateTime(), TickType.Close);
            // UInt32[] ts1LastWeekVolumes = ts1.GetRangeIncUint(new DateTime(), new DateTime(), TickType.Close);
        }

        internal TValue1 GetValues1(TKey dateTime, TickType p_tickType)
        {
            int i = IndexOfKey(dateTime);
            if (i < 0)
                throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, dateTime.ToString() ?? string.Empty));

            return values1[p_tickType][i];
        }

        internal TValue2 GetValues2(TKey dateTime, TickType p_tickType)
        {
            int i = IndexOfKey(dateTime);
            if (i < 0)
                throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, dateTime.ToString() ?? string.Empty));

            return values2[p_tickType][i];
        }


        public TKey[] GetKeyArrayDirect()
        {
            return keys;
        }

        // The most efficient, faster usage is the direct usage of the array. Better than (linked-) List, and there is no indirection.
        public TValue1[] GetValue1ArrayDirect(TickType p_tickType)
        {
            return values1[p_tickType];
        }

        public TValue2[] GetValue2ArrayDirect(TickType p_tickType)
        {
            return values2[p_tickType];
        }


        public FinTimeSeries()
        {
            keys = Array.Empty<TKey>();
            // the initial capacity for a Dictionary is 3. Later, it increases the capacity always to prime numbers.
            values1 = new Dictionary<TickType, TValue1[]>();
            values2 = new Dictionary<TickType, TValue2[]>();
            _size = 0;
            comparer = Comparer<TKey>.Default;

            valueList1 = new Dictionary<TickType, ValueList1?>();
            valueList2 = new Dictionary<TickType, ValueList2?>();
        }

        public FinTimeSeries(TickType[] tickTypes1, TickType[] tickTypes2) : this()
        {
            foreach (var tickType in tickTypes1)
            {
                values1.Add(tickType, Array.Empty<TValue1>());
            }
            foreach (var tickType in tickTypes2)
            {
                values2.Add(tickType, Array.Empty<TValue2>());
            }
        }

        // This should be ordered, otherwise, there will be problems if we want to extend it.
        public FinTimeSeries(TKey[] p_key, KeyValuePair<TickType, TValue1[]>[] p_values1, KeyValuePair<TickType, TValue2[]>[] p_values2) : this()
        {
            for (int i = 0; i < p_key.Length - 1; i++)
            {
                Debug.Assert(comparer.Compare(p_key[i], p_key[i + 1]) <= 0); // previous Key should be less than next key
            }
            _size = p_key.Length;
            keys = p_key;

            foreach (var p_value1 in p_values1)
            {
                Debug.Assert(p_value1.Value.Length == _size);
                values1.Add(p_value1.Key, p_value1.Value);
                valueList1.Add(p_value1.Key, null);
            }
            foreach (var p_value2 in p_values2)
            {
                Debug.Assert(p_value2.Value.Length == _size);
                values2.Add(p_value2.Key, p_value2.Value);
                valueList2.Add(p_value2.Key, null);
            }
        }

        // public FinTimeSeries(int capacity)
        // {
        //     if (capacity < 0)
        //         throw new ArgumentOutOfRangeException(nameof(capacity), capacity, SR.ArgumentOutOfRange_NeedNonNegNum);
        //     keys = new TKey[capacity];
        //     values = new TValue[capacity];
        //     comparer = Comparer<TKey>.Default;
        // }

        public FinTimeSeries(IComparer<TKey>? comparer)
            : this()
        {
            if (comparer != null)
            {
                this.comparer = comparer;
            }
        }

        // public FinTimeSeries(int capacity, IComparer<TKey>? comparer)
        //     : this(comparer)
        // {
        //     Capacity = capacity;
        // }

        // public FinTimeSeries(IDictionary<TKey, TValue> dictionary)
        //     : this(dictionary, null)
        // {
        // }

        // public FinTimeSeriesSortedList(IDictionary<TKey, TValue> dictionary, IComparer<TKey>? comparer)
        //     : this((dictionary != null ? dictionary.Count : 0), comparer)
        // {
        //     if (dictionary == null)
        //         throw new ArgumentNullException(nameof(dictionary));

        //     int count = dictionary.Count;
        //     if (count != 0)
        //     {
        //         TKey[] keys = this.keys;
        //         dictionary.Keys.CopyTo(keys, 0);
        //         dictionary.Values.CopyTo(values, 0);
        //         Debug.Assert(count == this.keys.Length);
        //         if (count > 1)
        //         {
        //             comparer = Comparer; // obtain default if this is null.
        //             Array.Sort<TKey, TValue>(keys, values, comparer);
        //             for (int i = 1; i != keys.Length; ++i)
        //             {
        //                 if (comparer.Compare(keys[i - 1], keys[i]) == 0)
        //                 {
        //                     throw new ArgumentException(SR.Format(SR.Argument_AddingDuplicate, keys[i]));
        //                 }
        //             }
        //         }
        //     }

        //     _size = count;
        // }

        public void Add1(TKey key, TickType tickType, TValue1 value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            int i = Array.BinarySearch<TKey>(keys, 0, _size, key, comparer);
            if (i >= 0)
                throw new ArgumentException(SR.Format(SR.Argument_AddingDuplicate, key), nameof(key));
            Insert1(~i, key, tickType, value);
        }

        public void Add2(TKey key, TickType tickType, TValue2 value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            int i = Array.BinarySearch<TKey>(keys, 0, _size, key, comparer);
            if (i >= 0)
                throw new ArgumentException(SR.Format(SR.Argument_AddingDuplicate, key), nameof(key));
            Insert2(~i, key, tickType, value);
        }


        // void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
        // {
        //     Add(keyValuePair.Key, keyValuePair.Value);
        // }

        // bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
        // {
        //     int index = IndexOfKey(keyValuePair.Key);
        //     if (index >= 0 && EqualityComparer<TValue>.Default.Equals(values[index], keyValuePair.Value))
        //     {
        //         return true;
        //     }
        //     return false;
        // }

        // bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
        // {
        //     int index = IndexOfKey(keyValuePair.Key);
        //     if (index >= 0 && EqualityComparer<TValue>.Default.Equals(values[index], keyValuePair.Value))
        //     {
        //         RemoveAt(index);
        //         return true;
        //     }
        //     return false;
        // }

        public int Capacity
        {
            get
            {
                return keys.Length;
            }
            set
            {
                if (value != keys.Length)
                {
                    if (value < _size)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value), value, SR.ArgumentOutOfRange_SmallCapacity);
                    }

                    if (value > 0)
                    {
                        TKey[] newKeys = new TKey[value];
                        if (_size > 0)
                        {
                            Array.Copy(keys, newKeys, _size);
                        }
                        keys = newKeys;


                        foreach (var kvPair in values1)
                        {
                            TValue1[] newValues1 = new TValue1[value];
                            if (_size > 0)
                            {
                                Array.Copy(kvPair.Value, newValues1, _size);
                            }
                            values1[kvPair.Key] = newValues1;
                        }
                        foreach (var kvPair in values2)
                        {
                            TValue2[] newValues2 = new TValue2[value];
                            if (_size > 0)
                            {
                                Array.Copy(kvPair.Value, newValues2, _size);
                            }
                            values2[kvPair.Key] = newValues2;
                        }
                    }
                    else
                    {
                        keys = Array.Empty<TKey>();
                        values1 = new Dictionary<TickType, TValue1[]>();
                        values2 = new Dictionary<TickType, TValue2[]>();
                    }
                }
            }
        }

        public IComparer<TKey> Comparer
        {
            get
            {
                return comparer;
            }
        }

        // void Add1(object key, object? value)
        // {
        //     if (key == null)
        //         throw new ArgumentNullException(nameof(key));

        //     if (value == null && !(default(TValue1)! == null))    // null is an invalid value for Value types  // TODO-NULLABLE: default(T) == null warning (https://github.com/dotnet/roslyn/issues/34757)
        //         throw new ArgumentNullException(nameof(value));

        //     if (!(key is TKey))
        //         throw new ArgumentException(SR.Format(SR.Arg_WrongType, key, typeof(TKey)), nameof(key));

        //     if (!(value is TValue1) && value != null)            // null is a valid value for Reference Types
        //         throw new ArgumentException(SR.Format(SR.Arg_WrongType, value, typeof(TValue1)), nameof(value));

        //     Add1((TKey)key, (TValue1)value!);
        // }

        public int Count
        {
            get
            {
                return _size;
            }
        }

        public IList<TKey> Keys
        {
            get
            {
                return GetKeyListHelper();
            }
        }

        public IList<TValue1> Values1(TickType tickType)
        {
            return GetValueListHelper1(tickType);
        }

        private KeyList GetKeyListHelper()
        {
            if (keyList == null)
                keyList = new KeyList(this);
            return keyList;
        }

        private ValueList1 GetValueListHelper1(TickType tickType)
        {
            ValueList1? vl1 = valueList1[tickType];
            if (vl1 == null)
            {
                vl1 = new ValueList1(this, tickType);
                valueList1[tickType] = vl1;
            }
            return vl1;
        }

        bool IsSynchronized
        {
            get { return false; }
        }


        bool ICollection.IsSynchronized
        {
            get { return false; }
        }
        // Synchronization root for this object.
        object ICollection.SyncRoot => this;


        public void Clear()
        {
            // clear does not change the capacity
            version++;
            // Don't need to doc this but we clear the elements so that the gc can reclaim the references.
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
            {
                Array.Clear(keys, 0, _size);
            }
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue1>())
            {
                foreach (var kvPair in values1)
                {
                    Array.Clear(kvPair.Value, 0, _size);
                }

            }
            _size = 0;
        }


        bool Contains(object key)
        {
            if (IsCompatibleKey(key))
            {
                return ContainsKey((TKey)key);
            }
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            return IndexOfKey(key) >= 0;
        }

        public bool ContainsValue1(TickType tickType, TValue1 value)
        {
            return IndexOfValue1(tickType, value) >= 0;
        }

        public bool ContainsValue2(TickType tickType, TValue2 value)
        {
            return IndexOfValue2(tickType, value) >= 0;
        }

        void ICollection.CopyTo(Array array, int index)
        {
            throw new NotImplementedException();    // see SortedList.cs template
        }


        private const int MaxArrayLength = 0X7FEFFFFF;

        private void EnsureCapacity(int min)
        {
            int newCapacity = keys.Length == 0 ? DefaultCapacity : keys.Length * 2;
            // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newCapacity > MaxArrayLength) newCapacity = MaxArrayLength;
            if (newCapacity < min) newCapacity = min;
            Capacity = newCapacity;
        }


        private TValue1 GetByIndex1(TickType tickType, int index)
        {
            if (index < 0 || index >= _size)
                throw new ArgumentOutOfRangeException(nameof(index), index, SR.ArgumentOutOfRange_Index);
            return values1[tickType][index];
        }

        private TValue2 GetByIndex2(TickType tickType, int index)
        {
            if (index < 0 || index >= _size)
                throw new ArgumentOutOfRangeException(nameof(index), index, SR.ArgumentOutOfRange_Index);
            return values2[tickType][index];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
            //return new Enumerator(this, Enumerator.KeyValuePair);
        }


        private TKey GetKey(int index)
        {
            if (index < 0 || index >= _size)
                throw new ArgumentOutOfRangeException(nameof(index), index, SR.ArgumentOutOfRange_Index);
            return keys[index];
        }


        // This indexer cannot be applied to both value1, and value2. So, we use it for value1 only, which should be the most important to the caller.
        public TValue1 this[TKey key, TickType tickType]
        {
            get
            {
                int i = IndexOfKey(key);
                if (i >= 0)
                    return values1[tickType][i];

                throw new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, key.ToString() ?? string.Empty));
            }
            set
            {
                if (((object)key) == null) throw new ArgumentNullException(nameof(key));
                int i = Array.BinarySearch<TKey>(keys, 0, _size, key, comparer);
                if (i >= 0)
                {
                    values1[tickType][i] = value;
                    version++;
                    return;
                }
                Insert1(~i, key, tickType, value);
            }
        }

        public int IndexOfKey(TKey key)     // if there is an exact match. If date is not found (because it was weekend), it returns -1.
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            int ret = Array.BinarySearch<TKey>(keys, 0, _size, key, comparer);
            return ret >= 0 ? ret : -1;
        }

        public int IndexOfKeyOrBeforeKey(TKey key)  // If date is not found, because it is a weekend, it gives back the previous index, which is less.
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            int ret = Array.BinarySearch<TKey>(keys, 0, _size, key, comparer);
            // You can use the '~' to take the bitwise complement which will give you the index of the first item larger than the search item.
            if (ret < 0)
                ret = ~ret -1; // this is the item which is larger, so we have to take away -1 to get the item which is smaller (older Date)
            return ret;
        }

        public int IndexOfValue1(TickType tickType, TValue1 value)
        {
            return Array.IndexOf(values1[tickType], value, 0, _size);
        }

        public int IndexOfValue2(TickType tickType, TValue2 value)
        {
            return Array.IndexOf(values2[tickType], value, 0, _size);
        }

        private void Insert1(int index, TKey key, TickType tickType, TValue1 value)
        {
            if (_size == keys.Length) EnsureCapacity(_size + 1);
            if (index < _size)
            {
                Array.Copy(keys, index, keys, index + 1, _size - index);
                Array.Copy(values1[tickType], index, values1[tickType], index + 1, _size - index);
            }
            keys[index] = key;
            values1[tickType][index] = value;
            _size++;
            version++;
        }

        private void Insert2(int index, TKey key, TickType tickType, TValue2 value)
        {
            if (_size == keys.Length) EnsureCapacity(_size + 1);
            if (index < _size)
            {
                Array.Copy(keys, index, keys, index + 1, _size - index);
                Array.Copy(values2[tickType], index, values2[tickType], index + 1, _size - index);
            }
            keys[index] = key;
            values2[tickType][index] = value;
            _size++;
            version++;
        }


        public bool TryGetValue1(TKey key, TickType tickType, [MaybeNullWhen(false)] out TValue1 value)
        {
            int i = IndexOfKey(key);
            if (i >= 0)
            {
                value = values1[tickType][i];
                return true;
            }

            value = default(TValue1)!;
            return false;
        }

        public bool TryGetValue2(TKey key, TickType tickType, [MaybeNullWhen(false)] out TValue2 value)
        {
            int i = IndexOfKey(key);
            if (i >= 0)
            {
                value = values2[tickType][i];
                return true;
            }

            value = default(TValue2)!;
            return false;
        }


        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _size)
                throw new ArgumentOutOfRangeException(nameof(index), index, SR.ArgumentOutOfRange_Index);
            _size--;
            if (index < _size)
            {
                Array.Copy(keys, index + 1, keys, index, _size - index);

                foreach (var kvPair in values1)
                {
                    Array.Copy(values1[kvPair.Key], index + 1, values1[kvPair.Key], index, _size - index);
                }
                foreach (var kvPair in values2)
                {
                    Array.Copy(values2[kvPair.Key], index + 1, values2[kvPair.Key], index, _size - index);
                }

            }
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
            {
                keys[_size] = default(TKey)!;
            }
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue1>())
            {
                foreach (var kvPair in values1)
                {
                    values1[kvPair.Key][_size] = default(TValue1)!;
                }
            }
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue2>())
            {
                foreach (var kvPair in values2)
                {
                    values2[kvPair.Key][_size] = default(TValue2)!;
                }
            }
            version++;
        }


        public bool Remove(TKey key)
        {
            int i = IndexOfKey(key);
            if (i >= 0)
                RemoveAt(i);
            return i >= 0;
        }


        // Sets the capacity of this sorted list to the size of the sorted list.
        // This method can be used to minimize a sorted list's memory overhead once
        // it is known that no new elements will be added to the sorted list. To
        // completely clear a sorted list and release all memory referenced by the
        // sorted list, execute the following statements:
        //
        // SortedList.Clear();
        // SortedList.TrimExcess();
        public void TrimExcess()
        {
            int threshold = (int)(((double)keys.Length) * 0.9);
            if (_size < threshold)
            {
                Capacity = _size;
            }
        }

        private static bool IsCompatibleKey(object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return (key is TKey);
        }


        private sealed class FinTimeSeriesKeyEnumerator : IEnumerator<TKey>, IEnumerator
        {
            private readonly FinTimeSeries<TKey, TValue1, TValue2> _sortedList;
            private int _index;
            private readonly int _version;
            [AllowNull] private TKey _currentKey = default!;

            internal FinTimeSeriesKeyEnumerator(FinTimeSeries<TKey, TValue1, TValue2> sortedList)
            {
                _sortedList = sortedList;
                _version = sortedList.version;
            }

            public void Dispose()
            {
                _index = 0;
                _currentKey = default;
            }

            public bool MoveNext()
            {
                if (_version != _sortedList.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }

                if ((uint)_index < (uint)_sortedList.Count)
                {
                    _currentKey = _sortedList.keys[_index];
                    _index++;
                    return true;
                }

                _index = _sortedList.Count + 1;
                _currentKey = default;
                return false;
            }

            public TKey Current
            {
                get
                {
                    return _currentKey;
                }
            }

            object? IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _sortedList.Count + 1))
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }

                    return _currentKey;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _sortedList.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }
                _index = 0;
                _currentKey = default;
            }
        }


        private sealed class FinTimeSeriesValueEnumerator1 : IEnumerator<TValue1>, IEnumerator
        {
            private readonly FinTimeSeries<TKey, TValue1, TValue2> _sortedList;

            TickType _tickType;
            private int _index;
            private readonly int _version;
            [AllowNull] private TValue1 _currentValue = default!;

            internal FinTimeSeriesValueEnumerator1(FinTimeSeries<TKey, TValue1, TValue2> sortedList, TickType tickType)
            {
                _sortedList = sortedList;
                _version = sortedList.version;
                _tickType = tickType;
            }

            public void Dispose()
            {
                _index = 0;
                _currentValue = default;
            }

            public bool MoveNext()
            {
                if (_version != _sortedList.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }

                if ((uint)_index < (uint)_sortedList.Count)
                {
                    _currentValue = _sortedList.values1[_tickType][_index];
                    _index++;
                    return true;
                }

                _index = _sortedList.Count + 1;
                _currentValue = default;
                return false;
            }

            public TValue1 Current
            {
                get
                {
                    return _currentValue;
                }
            }

            object? IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _sortedList.Count + 1))
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }

                    return _currentValue;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _sortedList.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }
                _index = 0;
                _currentValue = default;
            }
        }

        private sealed class FinTimeSeriesValueEnumerator2 : IEnumerator<TValue2>, IEnumerator
        {
            private readonly FinTimeSeries<TKey, TValue1, TValue2> _sortedList;

            TickType _tickType;
            private int _index;
            private readonly int _version;
            [AllowNull] private TValue2 _currentValue = default!;

            internal FinTimeSeriesValueEnumerator2(FinTimeSeries<TKey, TValue1, TValue2> sortedList, TickType tickType)
            {
                _sortedList = sortedList;
                _version = sortedList.version;
                _tickType = tickType;
            }

            public void Dispose()
            {
                _index = 0;
                _currentValue = default;
            }

            public bool MoveNext()
            {
                if (_version != _sortedList.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }

                if ((uint)_index < (uint)_sortedList.Count)
                {
                    _currentValue = _sortedList.values2[_tickType][_index];
                    _index++;
                    return true;
                }

                _index = _sortedList.Count + 1;
                _currentValue = default;
                return false;
            }

            public TValue2 Current
            {
                get
                {
                    return _currentValue;
                }
            }

            object? IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _sortedList.Count + 1))
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }

                    return _currentValue;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _sortedList.version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }
                _index = 0;
                _currentValue = default;
            }
        }






        [DebuggerDisplay("Count = {Count}")]
        [Serializable]
        public sealed class KeyList : IList<TKey>, ICollection
        {
            private readonly FinTimeSeries<TKey, TValue1, TValue2> _dict; // Do not rename (binary serialization)

            internal KeyList(FinTimeSeries<TKey, TValue1, TValue2> dictionary)
            {
                _dict = dictionary;
            }

            public int Count
            {
                get { return _dict._size; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            object ICollection.SyncRoot
            {
                get { return ((ICollection)_dict).SyncRoot; }
            }

            public void Add(TKey key)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public void Clear()
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public bool Contains(TKey key)
            {
                return _dict.ContainsKey(key);
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                // defer error checking to Array.Copy
                Array.Copy(_dict.keys, 0, array, arrayIndex, _dict.Count);
            }

            void ICollection.CopyTo(Array array, int arrayIndex)
            {
                if (array != null && array.Rank != 1)
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported, nameof(array));

                try
                {
                    // defer error checking to Array.Copy
                    Array.Copy(_dict.keys, 0, array!, arrayIndex, _dict.Count);
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException(SR.Argument_InvalidArrayType, nameof(array));
                }
            }

            public void Insert(int index, TKey value)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public TKey this[int index]
            {
                get
                {
                    return _dict.GetKey(index);
                }
                set
                {
                    throw new NotSupportedException(SR.NotSupported_KeyCollectionSet);
                }
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                return new FinTimeSeriesKeyEnumerator(_dict);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new FinTimeSeriesKeyEnumerator(_dict);
            }

            public int IndexOf(TKey key)
            {
                if (((object)key) == null)
                    throw new ArgumentNullException(nameof(key));

                int i = Array.BinarySearch<TKey>(_dict.keys, 0,
                                          _dict.Count, key, _dict.comparer);
                if (i >= 0) return i;
                return -1;
            }

            public bool Remove(TKey key)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
                // return false;
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }
        }

        [DebuggerDisplay("Count = {Count}")]
        [Serializable]
        public sealed class ValueList1 : IList<TValue1>, ICollection
        {
            private readonly FinTimeSeries<TKey, TValue1, TValue2> _dict; // Do not rename (binary serialization)
            private TickType _tickType;

            internal ValueList1(FinTimeSeries<TKey, TValue1, TValue2> dictionary, TickType tickType)
            {
                _dict = dictionary;
                _tickType = tickType;
            }

            public int Count
            {
                get { return _dict._size; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            object ICollection.SyncRoot
            {
                get { return ((ICollection)_dict).SyncRoot; }
            }

            public void Add(TValue1 key)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public void Clear()
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public bool Contains(TValue1 value)
            {
                return _dict.ContainsValue1(_tickType, value);
            }

            public void CopyTo(TValue1[] array, int arrayIndex)
            {
                // defer error checking to Array.Copy
                Array.Copy(_dict.values1[_tickType], 0, array, arrayIndex, _dict.Count);
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array != null && array.Rank != 1)
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported, nameof(array));

                try
                {
                    // defer error checking to Array.Copy
                    Array.Copy(_dict.values1[_tickType], 0, array!, index, _dict.Count);
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException(SR.Argument_InvalidArrayType, nameof(array));
                }
            }

            public void Insert(int index, TValue1 value)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public TValue1 this[int index]
            {
                get
                {
                    return _dict.GetByIndex1(_tickType, index);
                }
                set
                {
                    throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
                }
            }

            public IEnumerator<TValue1> GetEnumerator()
            {
                return new FinTimeSeriesValueEnumerator1(_dict, _tickType);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new FinTimeSeriesValueEnumerator1(_dict, _tickType);
            }

            public int IndexOf(TValue1 value)
            {
                return Array.IndexOf(_dict.values1[_tickType], value, 0, _dict.Count);
            }

            public bool Remove(TValue1 value)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
                // return false;
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }
        }

        [DebuggerDisplay("Count = {Count}")]
        [Serializable]
        public sealed class ValueList2 : IList<TValue2>, ICollection
        {
            private readonly FinTimeSeries<TKey, TValue1, TValue2> _dict; // Do not rename (binary serialization)
            private TickType _tickType;

            internal ValueList2(FinTimeSeries<TKey, TValue1, TValue2> dictionary, TickType tickType)
            {
                _dict = dictionary;
                _tickType = tickType;
            }

            public int Count
            {
                get { return _dict._size; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            object ICollection.SyncRoot
            {
                get { return ((ICollection)_dict).SyncRoot; }
            }

            public void Add(TValue2 key)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public void Clear()
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public bool Contains(TValue2 value)
            {
                return _dict.ContainsValue2(_tickType, value);
            }

            public void CopyTo(TValue2[] array, int arrayIndex)
            {
                // defer error checking to Array.Copy
                Array.Copy(_dict.values2[_tickType], 0, array, arrayIndex, _dict.Count);
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array != null && array.Rank != 1)
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported, nameof(array));

                try
                {
                    // defer error checking to Array.Copy
                    Array.Copy(_dict.values2[_tickType], 0, array!, index, _dict.Count);
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException(SR.Argument_InvalidArrayType, nameof(array));
                }
            }

            public void Insert(int index, TValue2 value)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }

            public TValue2 this[int index]
            {
                get
                {
                    return _dict.GetByIndex2(_tickType, index);
                }
                set
                {
                    throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
                }
            }

            public IEnumerator<TValue2> GetEnumerator()
            {
                return new FinTimeSeriesValueEnumerator2(_dict, _tickType);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new FinTimeSeriesValueEnumerator2(_dict, _tickType);
            }

            public int IndexOf(TValue2 value)
            {
                return Array.IndexOf(_dict.values2[_tickType], value, 0, _dict.Count);
            }

            public bool Remove(TValue2 value)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
                // return false;
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException(SR.NotSupported_SortedListNestedWrite);
            }
        }

    }


}