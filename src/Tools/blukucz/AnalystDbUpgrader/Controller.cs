using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using SqCommon;
using YahooFinanceApi;

namespace YahooCrawler
{
    public class YFRecord
    {
        public string Date { get; set; } = string.Empty;
        public float AdjClose { get; set; }
        public float Close { get; set; }
    }

    public enum Universe : byte
    {
        Unknown = 0,
        SP500,
        Nasdaq100,
        GC1,
        GC2,
        GC3,
        ARK
    }

    public class TickerMembers
    {
        public string Ticker { get; set; } = string.Empty;
        public Universe Universe { get; set; } = Universe.Unknown;
    }

    public class OrigRecomm
    {
        [Name("date")]
        public string Date { get; set; } = string.Empty;
        [Name("ticker")]
        public string Ticker { get; set; } = string.Empty;
        [Name("companyName")]
        public string CompanyName { get; set; } = string.Empty;
        [Name("action")]
        public string Action { get; set; } = string.Empty;
        [Name("initiater")]
        public string Initiater { get; set; } = string.Empty;
        [Name("priceTarget")]
        public string PriceTarget { get; set; } = string.Empty;
        [Name("priceTargetFrom")]
        public string PriceTargetFrom { get; set; } = string.Empty;
        [Name("currency")]
        public string Currency { get; set; } = string.Empty;
        [Name("rating")]
        public string Rating { get; set; } = string.Empty;
        [Name("ratingFrom")]
        public string RatingFrom { get; set; } = string.Empty;
        [Name("ratingNumber")]
        public string RatingNumber { get; set; } = string.Empty;
        [Name("impactNumber")]
        public string ImpactNumber { get; set; } = string.Empty;
    }

    public class OutputRecomm : OrigRecomm
    {
        public float AdjClose126db { get; set; }
        public float AdjClose63db { get; set; }
        public float AdjClose21db { get; set; }
        public float AdjClose10db { get; set; }
        public float AdjClose5db { get; set; }
        public float AdjClose3db { get; set; }
        public float AdjClose1db { get; set; }
        public float AdjClose0db { get; set; } //Day T-1
        public float AdjClose0da { get; set; }
        public float AdjClose1da { get; set; }
        public float AdjClose2da { get; set; }
        public float AdjClose3da { get; set; }
        public float AdjClose4da { get; set; }
        public float AdjClose5da { get; set; }
        public float AdjClose10da { get; set; }
        public float AdjClose15da { get; set; }
        public float AdjClose21da { get; set; }
        public float AdjClose63da { get; set; }
        public float AdjClose126da { get; set; }
        public float AdjClose252da { get; set; }
        public float Close {get; set;}

        public OutputRecomm(OrigRecomm parentToCopy)
        {
            this.Date = parentToCopy.Date;
            this.Ticker = parentToCopy.Ticker;
            this.CompanyName = parentToCopy.CompanyName;
            this.Action = parentToCopy.Action;
            this.Initiater = parentToCopy.Initiater;
            this.PriceTarget = parentToCopy.PriceTarget;
            this.PriceTargetFrom = parentToCopy.PriceTargetFrom;
            this.Currency = parentToCopy.Currency;
            this.Rating = parentToCopy.Rating;
            this.RatingFrom = parentToCopy.RatingFrom;
            this.RatingNumber = parentToCopy.RatingNumber;
            this.ImpactNumber = parentToCopy.ImpactNumber;
        }
    }
    class Controller
    {
        static public Controller g_controller = new();

        string[] m_universeTickers = Array.Empty<string>();
        OrigRecomm[] m_recommRecords = Array.Empty<OrigRecomm>();

        OrigRecomm[] m_slimmedRecommRecords = Array.Empty<OrigRecomm>();
        List<OutputRecomm> m_outputRec = new();

        IDictionary<string, List<YFRecord>> m_yfDataFromCsv = new Dictionary<string, List<YFRecord>>();

        internal void Init()
        {
        }

        internal void Exit()
        {
        }


        public void ReadTickerUniverse()
        {
            using (var reader = new StreamReader("D:\\Temp\\YFHist\\Tickers.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                List<TickerMembers> tickerMembers = csv.GetRecords<TickerMembers>().ToList();
                m_universeTickers = tickerMembers.Select(r => r.Ticker).ToArray();
            }
        }

        public async void DownloadYFtoCsv()
        {
            ReadTickerUniverse();
            foreach (var ticker in m_universeTickers)
            {
                DateTime expectedHistoryStartDateET = new(2010, 1, 1);
                IReadOnlyList<Candle?>? history = await Yahoo.GetHistoricalAsync(ticker, expectedHistoryStartDateET, DateTime.Now, Period.Daily);

                YFRecord[] yfRecords = history.Select(r => new YFRecord() { Date = Utils.Date2hYYYYMMDD(r!.DateTime), AdjClose = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 4), Close = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.Close, 4) }).ToArray();

                using (var writer = new StreamWriter("D:\\Temp\\YFHist\\" + ticker + ".csv"))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(yfRecords);
                }
            }
        }

        public void ReadRecommendationsCsv()
        {
            using (var reader = new StreamReader("D:\\Temp\\All_20210330.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                m_recommRecords = csv.GetRecords<OrigRecomm>().ToList().ToArray();
            }
        }

        public void TransformRecommendationsCsv()
        {
            ReadTickerUniverse();
            ReadRecommendationsCsv();
            ReadYahooCsvFiles();

            m_slimmedRecommRecords = m_recommRecords.Where(r => (r.Currency == "$" || String.IsNullOrEmpty(r.Currency)) && (m_universeTickers.Contains(r.Ticker))).ToArray();
            // m_outputRec = m_slimmedRecommRecords.Select(parent => new OutputRecomm(parent)).ToArray();
            m_outputRec = new List<OutputRecomm> (m_slimmedRecommRecords.Length);

            foreach (var item in m_slimmedRecommRecords)
            {
                List<YFRecord> yfPrices = m_yfDataFromCsv[item.Ticker];
                int iDay = yfPrices.FindIndex(x => string.Compare(x.Date, item.Date) > -1);
                int iDay126b = Math.Max(iDay - 127, 0);
                int iDay63b = Math.Max(iDay - 64, 0);
                int iDay21b = Math.Max(iDay - 22, 0);
                int iDay10b = Math.Max(iDay - 11, 0);
                int iDay5b = Math.Max(iDay - 6, 0);
                int iDay3b = Math.Max(iDay - 4, 0);
                int iDay1b = Math.Max(iDay - 2, 0);
                int iDay0b = Math.Max(iDay - 1, 0);
                int iDay0a = iDay;
                int iDay1a = iDay + 1 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 1;
                int iDay2a = iDay + 2 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 2;
                int iDay3a = iDay + 3 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 3;
                int iDay4a = iDay + 4 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 4;
                int iDay5a = iDay + 5 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 5;
                int iDay10a = iDay + 10 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 10;
                int iDay15a = iDay + 15 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 15;
                int iDay21a = iDay + 21 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 21;
                int iDay63a = iDay + 63 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 63;
                int iDay126a = iDay + 126 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 126;
                int iDay252a = iDay + 252 >= yfPrices.Count ? yfPrices.Count - 1 : iDay + 252;

                OutputRecomm newRec = new(item);
                newRec.AdjClose126db = yfPrices[iDay126b].AdjClose;
                newRec.AdjClose63db = yfPrices[iDay63b].AdjClose;
                newRec.AdjClose21db = yfPrices[iDay21b].AdjClose;
                newRec.AdjClose10db = yfPrices[iDay10b].AdjClose;
                newRec.AdjClose5db = yfPrices[iDay5b].AdjClose;
                newRec.AdjClose3db = yfPrices[iDay3b].AdjClose;
                newRec.AdjClose1db = yfPrices[iDay1b].AdjClose;
                newRec.AdjClose0db = yfPrices[iDay0b].AdjClose;
                newRec.AdjClose0da = yfPrices[iDay0a].AdjClose;
                newRec.AdjClose1da = yfPrices[iDay1a].AdjClose;
                newRec.AdjClose2da = yfPrices[iDay2a].AdjClose;
                newRec.AdjClose3da = yfPrices[iDay3a].AdjClose;
                newRec.AdjClose4da = yfPrices[iDay4a].AdjClose;
                newRec.AdjClose5da = yfPrices[iDay5a].AdjClose;
                newRec.AdjClose10da = yfPrices[iDay10a].AdjClose;
                newRec.AdjClose15da = yfPrices[iDay15a].AdjClose;
                newRec.AdjClose21da = yfPrices[iDay21a].AdjClose;
                newRec.AdjClose63da = yfPrices[iDay63a].AdjClose;
                newRec.AdjClose126da = yfPrices[iDay126a].AdjClose;
                newRec.AdjClose252da = yfPrices[iDay252a].AdjClose;
                newRec.Close = yfPrices[iDay0b].Close;

                m_outputRec.Add(newRec);
            }

            WriteOutputRecommToCsv();
        }
        private void ReadYahooCsvFiles()
        {
            foreach (var ticker in m_universeTickers)
            {
                using (var reader = new StreamReader("D:\\Temp\\YFHist\\" + ticker + ".csv"))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<YFRecord>().ToList();
                    m_yfDataFromCsv.Add(ticker, records);
                }
            }
        }
        private void WriteOutputRecommToCsv()
        {
            using (var writer = new StreamWriter("D:\\Temp\\YFHist\\outputRecommendations.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(m_outputRec);
            }
        }
    }
}