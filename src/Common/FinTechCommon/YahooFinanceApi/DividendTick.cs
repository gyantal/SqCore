using System;
using CsvHelper.Configuration.Attributes;

namespace YahooFinanceApi;

public sealed class DividendTick : ITick
{
    [Name("Date")]
    public DateTime DateTime { get; set; }

    [Name("Dividends")]
    public decimal Dividend { get; set; }
}
