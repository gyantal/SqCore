
// Minimize memory footprint: cache coherence for fast backtests and small memory requirement on Linux server, because Amazon cloud AWS RAM is expensive.
// We researched how others implement the TimeSeries structure on GitHub. But they all used basic things like for each Stock in a List<Stocks>, there is a List<DailyData>, where DailyData = (Date, OpenPrice, ClosePrice, Volume)
// Those too simple methods doesn't use cache coherence, therefore they are slow. And store Date 5000 times if there are 5000 stocks in database, so not memory efficient either. 
// They don't use QuickSort for getting Stock data quickly (so, if Db has 5000 stocks, it is O(N) to find the good one). Furthermore, List<> is a linked list, which is a disaster to walk, and consumes huge memory.
// Using Arrays are preferred instead of List<>. Not only because smaller memory requirement, but because ArrayCopy can be used instead of walking the List<> linked list.
// We try to address each of the points mentioned here.

// We create separate TimeSeries for 5minutes, daily, weekly, monthly time frequency.

// If Date is stored with ClosePrice, and then Date is stored with OpenPrice, High-LowPrice, Volume it is better to factor out the Date field. 
// Generalize it further. All the stocks contains more or less the same Date[]. It should be factored out, and stored only once into a shared Date field.
// Some dates could be missing in the middle for some specific stocks. No problem. Store NaN there.
// FinTimeSeries SumMem:  With 5000 stocks, 30years: 5000*260*30*(2+4)= 235MB
// CompactFinTimeSeries SumMem: With 5000 stocks, 30years: 2*260*30+5000*260*30*(4)= 156MB (a saving of 90MB)
// So, in general the saving is 100MB, calculated as 5000 * 30 * 260 * 2 = 80MB, but in the future it will be more.
// This 100MB saving does not increase when we add more daily data such as Open/High/Low/Volume.
// But CompactFinTimeSeries gives better cache usage, because the shared Date field page is always in the cache, because it is used frequently.

// The huge adventage of the shared Date[] is that clients don't have to bother about synchronyzing the different stock.dates.
// It happens frequently that there are missing dates in the data. Some ETFs have holes in them.
// In the QuickTester it was a problem that when we queried the last 2 years data, some ETF had 520 data days, other had only 519. 
// The client had to screen this and synch those dates and correct it. Which is cumbersome. Now, it is done in a global level. Clients are happier and their execution is faster.

// To summarize. The adventage of the single shared date[]:
// 1. A fix 100MB RAM saving for 5000 stocks
// 2. Cache coherence: shared date[] is always in cache page. 10-20% faster execution
// 3. Clients are worry free about holes in the data. Missing dates for some stocks. They don't have to synch start dates, end dates.
// However, this shared date[] can only be implemented if the latest date is the first (index 0) item. Its reverse order is a bit cumbersome to use, but client can get used to it.

// **************** MemDb to QuickTester: What is the fastest access of prices for QuickTester. That wants to get the only ClosePrice data between StartDate and EndDate
// >Time series usage example: 'g_MemDb["MSFT"].Dates'. Dates is naturally increasing. So, it should be an OrderedList, so finding an item is O(LogN), not O(N). Same for indEndDate.
// >DatesToQuickTester = MemArrayCopy(MemDb["MSFT"].Dates) between indStartDate to indEndDate). If Dates is not a standalone array, ArrayCopy would not be possible.
// >ClosePrices given To QuickTester: MemArrayCopy(MemDb["MSFT"].ClosePrices) between indStartDate to indEndDate can also work and fast.
// >Quicktester better ArrayCopy and clone this needed historical data, so it can manipulate privately (and there is no multithreading problem if MemDb refreshes it from YahooFinance at 8:00 or when it can.)
// There is no faster way of serving QuickTester from MemDB
// The fastest access is very similar to Dotnet SortedList, but instead of 1 value array, we have separate.
// https://github.com/dotnet/corefx/blob/master/src/System.Collections/src/System/Collections/Generic/SortedList.cs
// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Collections/src/System/Collections/Generic/SortedList.cs

// This is better smaller RAM storage as well than the alternative simplest List<Stock>(List<Date, DailyRecord>), 
// because DailyRecord would contain All potential TickType fields that is EVER used, consuming huge RAM
// Also Date, ClosePrice values are stored in Array, which is much faster than List

// Keep the TKey as parameter as: DateTime (a DateTime is a 8 byte struct, per millisecond data), DateTimeAsInt (4 byte, per minute data), DateOnly (2 byte) 

// When new date comes in, we increase the size, it should be multithread safe. 
// Either big locks (which is not effective, and error prone), or better, it clones the old Dict to a new pointer. And only when it is finally ready, it will swap the two pointers at the end. 
// The other processes, like GetData(), work inside not on an m_memberVariablePointer, but on the pointer that the got at the beginning as a parameter. 
// That way, they are consistent to their caller. They can use the old data, while the MemDb already has the new data. 
// Also, callers should assume that MemDb can swap internal data, so better call ALL the data in one function that is consistent and thread safe. 
// If they call different ticker-data in different calls, it might be possible that they get different dates. 
// Although if they specify exactly what date-range they want, then whatever, many different calls can be consistent as well.

// Dictionary is 1x..5x faster than ConcurrentDictionary, because it is do internal locks and also a lot of GC allocation.
// because we will only update data in the creator thread, we don't need concurrency, so ConcurrentDictionary is not required
// https://cc.davelozinski.com/c-sharp/dictionary-vs-concurrentdictionary
// https://www.tabsoverspaces.com/233590-concurrentdictionary-is-slow-or-is-it

// Design choice: give clients direct access to data via GetDataDirect(), instead of strict encapsulation and cloning data in client queries.
// An interface can be developed to Clone() data to clients. This is usually necessary in Databases (Sql), 
// because in SQL atomicity cannot be guaranteed any other way, as duplicate database is impossible, because of so much data on disk
// But it is much faster and less RAM if we let clients access data pointer directly with GetDataDirect()
// Data is not strictly encapsulated. There are no guards against clients changing internal data. But it was our design choice for giving the fastest access posible.
// public struct TsQuery<TKey>
// {
//     public uint AssetId;
//     public uint TickType;
//     public TKey StartDateInc;
//     public TKey EndDateInc;
// }
// var tsQuery = new TsQuery<DateOnly>(r.AssetId, TickType.SplitDivAdjClose, new DateOnly(), new DateOnly());
// var histData = MemDb.gMemDb.DailyHist.GetData(tsQuery);

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SqCommon;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FinTechCommon
{
    public class ReverseComparer<TKey> : IComparer<TKey>
    {
        public int Compare(TKey? x, TKey? y)
        {
            return Comparer<TKey>.Default.Compare(y, x);    // (x,y ) is reversed to (y,x)
        }
    }
    public class TsDateData<TKey, TAssetId, TValue1, TValue2>  where TKey : notnull where TAssetId : notnull
    {
        private int _size;
        public TKey[] Dates;    // dates are in reverse order. Dates[0] is today or yesterday

        public Dictionary<TAssetId, Tuple<Dictionary<TickType, TValue1[]>, Dictionary<TickType, TValue2[]>>> Data; // for every assetId there is a list of float[] and uint[]

        private readonly IComparer<TKey> comparer;

        public TsDateData(TKey[] p_dates, Dictionary<TAssetId, Tuple<Dictionary<TickType, TValue1[]>, Dictionary<TickType, TValue2[]>>> p_data)
        {
            Dates= p_dates;
            Data = p_data;
            _size = Dates.Length;

            comparer = new ReverseComparer<TKey>(); // Array.BinarySearch() requires array is sorted in ascending order according to the specified comparator. Our specific comparator is the ReverseComparer.
        }
        public int Count
        {
            get
            {
                return _size;
            }
        }

        public int IndexOfKey(TKey key)     // if there is an exact match. If date is not found (because it was weekend), it returns -1.
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            int ret = Array.BinarySearch<TKey>(Dates, 0, _size, key, comparer);
            return ret >= 0 ? ret : -1;
        }

        public int IndexOfKeyOrAfter(TKey key)  // If date is not found, because it is a weekend, it gives back the previous index, which is less.
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            int ret = Array.BinarySearch<TKey>(Dates, 0, _size, key, comparer);
            // You can use the '~' to take the bitwise complement which will give you the index of the first item larger than the search item.
            if (ret < 0)
                ret = ~ret; // this is the item which is smaller (older Date); it comes After the key.
            return ret;
        }

        public int MemUsed()
        {
            int memUsedData = Dates.Length * Marshal.SizeOf(typeof(TKey));  // Tkey = DateTime (8 byte), DateTimeAsInt (4 byte), DateOnly (2 byte)
            int tickTypeSize = Marshal.SizeOf(Enum.GetUnderlyingType(typeof(TickType)));    // Enum is implemented as int, so its size is 4
            foreach (var data in Data)
            {
                memUsedData += Marshal.SizeOf(typeof(TAssetId));
                foreach (var ts in data.Value.Item1)
                {
                    memUsedData += tickTypeSize + ts.Value.Length * Marshal.SizeOf(typeof(TValue1));
                }
                foreach (var ts in data.Value.Item2)
                {
                    memUsedData += tickTypeSize + ts.Value.Length * Marshal.SizeOf(typeof(TValue2));
                }
            }
            return memUsedData;
        }
    }

    // see SortedList<TKey,TValue> as template https://github.com/dotnet/corefx/blob/master/src/System.Collections/src/System/Collections/Generic/SortedList.cs
    [DebuggerDisplay("Count = {m_data.Count}")]
    [Serializable]
    public class CompactFinTimeSeries<TKey, TAssetId, TValue1, TValue2> where TKey : notnull where TAssetId : notnull   // Tkey = DateTime (8 byte), DateTimeAsInt (4 byte), DateOnly (2 byte), or any int, byte (1 byte), or even string (that can be ordered)
    {
        TsDateData<TKey, TAssetId, TValue1, TValue2> m_data;      // this m_data pointer can be swapped in an atomic instruction after update

        static void HowToUseThisClassExamples()
        {
            DateOnly[] dates = new DateOnly[2] { new DateOnly(2020, 05, 05), new DateOnly(2020, 05, 06)};
            var dict1 = new Dictionary<TickType, float[]>() { { TickType.SplitDivAdjClose, new float[2] { 10.1f, 12.1f } } };
            var dict2 = new Dictionary<TickType, uint[]>();
            uint assetId = 1;
            var data = new Dictionary<uint, Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>>()
                    { { assetId, new Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>(dict1, dict2)}};

            var ts1 = new CompactFinTimeSeries<DateOnly, uint, float, uint>();
            ts1.ChangeData(dates, data);
        }

        public CompactFinTimeSeries()
        {
            var values = new Dictionary<TAssetId, Tuple<Dictionary<TickType, TValue1[]>, Dictionary<TickType, TValue2[]>>>();
            TsDateData<TKey, TAssetId, TValue1, TValue2> data = new TsDateData<TKey, TAssetId, TValue1, TValue2>(new TKey[0], values);
            m_data = data;  // 64 bit values are atomic on x64
        }

        public CompactFinTimeSeries(TKey[] p_dates, Dictionary<TAssetId, Tuple<Dictionary<TickType, TValue1[]>, Dictionary<TickType, TValue2[]>>> p_data)
        {
            m_data = new TsDateData<TKey, TAssetId, TValue1, TValue2>(p_dates, p_data);
        }

        // ChangeData() will replace a pointer in an atomic way
        public void ChangeData(TKey[] p_dates, Dictionary<TAssetId, Tuple<Dictionary<TickType, TValue1[]>, Dictionary<TickType, TValue2[]>>> p_data)
        {
            TsDateData<TKey, TAssetId, TValue1, TValue2> data = new TsDateData<TKey, TAssetId, TValue1, TValue2>(p_dates, p_data);
            m_data = data;  // 64 bit values are atomic on x64
        }

        // Faster and more memory efficient if clients can get direct access to data pointer, instead of duplicating data.
        // Daily ChangeData() can update pointer to new data, but that is not a problem. Clients got the old pointer, but that data is consistent.
        public TsDateData<TKey, TAssetId, TValue1, TValue2> GetDataDirect()
        {
            return m_data;
        }
    }

}