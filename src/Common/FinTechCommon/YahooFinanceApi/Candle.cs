using System;
using CsvHelper.Configuration.Attributes;

namespace YahooFinanceApi
{
    public sealed class Candle: ITick
    {
        [Name("Date")]
        public DateTime DateTime { get; set; }
        [Name("Open")]
        public decimal Open { get; set; }
        [Name("High")]
        public decimal High { get; set; }
        [Name("Low")]
        public decimal Low { get; set; }
        [Name("Close")]
        public decimal Close { get; set; }
        [Name("Adj Close")]
        public decimal AdjustedClose { get; set; }
        [Name("Volume")]
        public long Volume { get; set; }

    }
}
