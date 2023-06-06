using System;
using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.CSharp
{
    class QcPrice
    {
        public DateTime ReferenceDate;
        public decimal Close;
    }

    class QcDividend
    {
        public DateTime ReferenceDate;
        public Dividend Dividend;
    }

    class QcSplit
    {
        public DateTime ReferenceDate;
        public Split Split;
    }

    class YfSplit
    {
        public DateTime ReferenceDate;
        public decimal SplitFactor;
    }

    class QCAlgorithmUtils
    {
        public static long DateTimeUtcToUnixTimeStamp(DateTime p_utcDate) // Int would roll over to a negative in 2038 (if you are using UNIX timestamp), so long is safer
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            TimeSpan span = p_utcDate - dtDateTime;
            return (long)span.TotalSeconds;
        }
    }
}