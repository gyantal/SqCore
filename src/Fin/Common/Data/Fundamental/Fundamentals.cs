using System;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Data.Fundamental
{
    /// <summary>
    /// Defines a merged viw of <see cref="FineFundamental"/> and <see cref="CoarseFundamental"/>
    /// </summary>
    public class Fundamentals : FineFundamental
    {
        /// <summary>
        /// Gets the market for this symbol
        /// </summary>
        public string Market { get; set; }

        /// <summary>
        /// Gets the day's dollar volume for this symbol
        /// </summary>
        public decimal DollarVolume { get; set; }

        /// <summary>
        /// Gets the day's total volume
        /// </summary>
        public long Volume { get; set; }

        /// <summary>
        /// Returns whether the symbol has fundamental data for the given date
        /// </summary>
        public bool HasFundamentalData { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public Fundamentals()
        {
            DataType = MarketDataType.Auxiliary;
        }

        /// <summary>
        /// Return the URL string source of the file. This will be converted to a stream
        /// </summary>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            throw new NotImplementedException($"{nameof(Fundamentals)}.{nameof(GetSource)} is never intended to be invoked.");
        }

        /// <summary>
        /// Reader converts each line of the data source into BaseData objects. Each data type creates its own factory method, and returns a new instance of the object
        /// each time it is called. The returned object is assumed to be time stamped in the config.ExchangeTimeZone.
        /// </summary>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            throw new NotImplementedException($"{nameof(Fundamentals)}.{nameof(Reader)} is never intended to be invoked.");
        }
    }
}