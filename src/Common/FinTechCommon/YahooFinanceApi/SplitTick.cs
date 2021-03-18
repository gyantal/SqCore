using System;
using CsvHelper.Configuration.Attributes;

namespace YahooFinanceApi
{
    public sealed class SplitTick : ITick
    {
        [Name("Date")]
        public DateTime DateTime { get; set; }
        [Name("Stock Splits")]
        public string StockSplits { get; set; } = string.Empty;

        public decimal BeforeSplit { get; set; }

        public decimal AfterSplit { get; set; }
    }
}
