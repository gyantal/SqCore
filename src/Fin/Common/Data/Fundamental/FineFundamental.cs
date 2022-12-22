using System;
using System.IO;
using Newtonsoft.Json;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Data.Fundamental
{
    /// <summary>
    /// Definition of the FineFundamental class
    /// </summary>
    public partial class FineFundamental
    {
        /// <summary>
        /// The end time of this data.
        /// </summary>
        [JsonIgnore]
        public override DateTime EndTime
        {
            get { return Time + QuantConnect.Time.OneDay; }
            set { Time = value - QuantConnect.Time.OneDay; }
        }

        /// <summary>
        /// Price * Total SharesOutstanding.
        /// The most current market cap for example, would be the most recent closing price x the most recent reported shares outstanding.
        /// For ADR share classes, market cap is price * (ordinary shares outstanding / adr ratio).
        /// </summary>
        [JsonIgnore]
        public long MarketCap => CompanyProfile?.MarketCap ?? 0;


        /// <summary>
        /// Creates the universe symbol used for fine fundamental data
        /// </summary>
        /// <param name="market">The market</param>
        /// <param name="addGuid">True, will add a random GUID to allow uniqueness</param>
        /// <returns>A fine universe symbol for the specified market</returns>
        public static Symbol CreateUniverseSymbol(string market, bool addGuid = true)
        {
            market = market.ToLowerInvariant();
            var ticker = $"qc-universe-fine-{market}";
            if (addGuid)
            {
                ticker += $"-{Guid.NewGuid()}";
            }
            var sid = SecurityIdentifier.GenerateEquity(SecurityIdentifier.DefaultDate, ticker, market);
            return new Symbol(sid, ticker);
        }

        /// <summary>
        /// Return the URL string source of the file. This will be converted to a stream
        /// </summary>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            var source =
                Path.Combine(Globals.CacheDataFolder, Invariant(
                    $"equity/{config.Market}/fundamental/fine/{config.Symbol.Value.ToLowerInvariant()}/{date:yyyyMMdd}.zip"
                ));

            return new SubscriptionDataSource(source, SubscriptionTransportMedium.LocalFile, FileFormat.Csv);
        }

        /// <summary>
        /// Reader converts each line of the data source into BaseData objects. Each data type creates its own factory method, and returns a new instance of the object
        /// each time it is called. The returned object is assumed to be time stamped in the config.ExchangeTimeZone.
        /// </summary>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            var data = JsonConvert.DeserializeObject<FineFundamental>(line);

            data.DataType = MarketDataType.Auxiliary;
            data.Symbol = config.Symbol;
            data.Time = date;

            return data;
        }
    }
}
