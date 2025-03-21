#region imports
using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using Fin.Base;
using SqCommon;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using System.Text;
using System.Globalization;
using System.IO;
using System.Web;
using System.Collections.Specialized;
#endregion
namespace QuantConnect.Algorithm.CSharp
{
    public class SqTradeAccumulation : QCAlgorithm
    {
        StartDateAutoCalcMode _startDateAutoCalcMode = StartDateAutoCalcMode.Unknown;
        DateTime _forcedStartDateTimeUtc; // user can force a startdate. Work UtcTime with Time component everywhere internally. Utc vs. Loc usage: see doc "C# DateTime.txt"
        DateTime _startDateTimeUtc = DateTime.MinValue; // "2025-01-13T08:00Z", real startDate. We expect PV chart to start from here. There can be some warmUp days before that, for which data is needed.
        DateTime _forcedEndDateTimeUtc;
        DateTime _endDateTimeUtc = DateTime.MaxValue; // "2025-01-13T23:59Z"
        TimeSpan _warmUp = TimeSpan.Zero;
        OrderTicket _lastOrderTicket = null;
        private Dictionary<DateTime, List<Trade>> _tradesByDateOnly; //  Contains only Buy and Sell trades. Same as this.PortTradeHist but that is a List, this is a Dict.
        private Dictionary<DateTime, List<Trade>> _cashTransactionsByDateOnly; // Contains only Deposit and Withdrawal
        private HashSet<string> _tickers; // Set of unique symbols traded
        private List<DateTime> _tradeDateUtcs; // Sorted list of trade dates
        private List<DateTime> _cashDateUtcs; // Sorted list of cash transaction dates
        // private List<Trade> _executedTrades;
        private decimal _initialCash;
        private decimal _yestPV;
        private List<(DateTime date, decimal pvBefore, decimal pvAfter, decimal cash)> _portfolioValues;

        public override void Initialize()
        {
            NameValueCollection algorithmParamQuery = HttpUtility.ParseQueryString(AlgorithmParam); // forcedEndDate comes as UTC usually as "2025-01-13T23:59Z"
            QCAlgorithmUtils.ProcessAlgorithmParam(algorithmParamQuery, out _forcedStartDateTimeUtc, out _forcedEndDateTimeUtc, out _startDateAutoCalcMode);

            _tradesByDateOnly = new Dictionary<DateTime, List<Trade>>();
            _cashTransactionsByDateOnly = new Dictionary<DateTime, List<Trade>>();
            // _executedTrades = new List<Trade>();
            _portfolioValues = new List<(DateTime date, decimal pvBefore, decimal pvAfter, decimal cash)>();

            // Populate the _tradesByDate, _cashTransactionsByDate dictionaries based on the type of transaction
            DateTime prevDate = DateTime.MinValue; // For checking if the trades are sorted by date
            foreach (Fin.Base.Trade? trade in this.PortTradeHist)
            {
                if (trade.AssetType == AssetType.Option) // TEMP: disable options for now as "TMF 231215C00064000" causes exceptions
                    continue;
                if (trade.Time < prevDate) // We Assume that trades are sotred by date. Hard requirement. If not, we don't process further, forcing the user to change the trades. This is for the purpose of fast execution.
                    throw new Exception("Tradelist (PortfolioTradeHistory) is not sorted by date.");
                prevDate = trade.Time;
                DateTime tradeDateOnlyUtc = trade.Time.Date;
                switch (trade.Action)
                {
                    case TradeAction.Buy:
                    case TradeAction.Sell:
                        if (!_tradesByDateOnly.ContainsKey(tradeDateOnlyUtc))
                            _tradesByDateOnly[tradeDateOnlyUtc] = new List<Trade>();
                        _tradesByDateOnly[tradeDateOnlyUtc].Add(trade);
                        break;
                    case TradeAction.Deposit:
                    case TradeAction.Withdrawal:
                        if (!_cashTransactionsByDateOnly.ContainsKey(tradeDateOnlyUtc))
                            _cashTransactionsByDateOnly[tradeDateOnlyUtc] = new List<Trade>();
                        _cashTransactionsByDateOnly[tradeDateOnlyUtc].Add(trade);
                        break;
                    default:
                        continue;
                }
            }

            _tradeDateUtcs = new List<DateTime>(_tradesByDateOnly.Keys);
            _cashDateUtcs = new List<DateTime>(_cashTransactionsByDateOnly.Keys);

            // Set initial cash and start date based on the earliest cash transaction date
            DateTime earliestCashDateOnlyUtc = DateTime.MinValue;
            if (_cashDateUtcs.Count > 0)
            {
                DateTime firstCashDate = _cashDateUtcs[0];
                List<Trade> firstCashTransactions = _cashTransactionsByDateOnly[firstCashDate];

                // Calculate the _initialCash from the first day's transactions
                _initialCash = 0m;
                foreach (Trade trade in firstCashTransactions)
                {
                    if (trade.Action == TradeAction.Deposit)
                        _initialCash += (decimal)trade.Price;
                    else if (trade.Action == TradeAction.Withdrawal)
                        _initialCash -= (decimal)trade.Price;
                }

                if (_initialCash <= 0)
                    throw new Exception("Initial cash balance is not positive.");

                earliestCashDateOnlyUtc = firstCashDate;
                _cashDateUtcs.RemoveAt(0);
                _cashTransactionsByDateOnly.Remove(firstCashDate); // Remove the processed date
            }
            else
                throw new Exception("No cash transactions found.");

            // startDate and warmup determination
            SetWarmUp(_warmUp);

            if (_forcedStartDateTimeUtc == DateTime.MinValue) // auto calculate if user didn't give a forced startDate. Otherwise, we are obliged to use that user specified forced date.
                _startDateTimeUtc = (_tradeDateUtcs.Count > 0 && _tradeDateUtcs[0] < earliestCashDateOnlyUtc) ? _tradeDateUtcs[0].AddHours(8) : earliestCashDateOnlyUtc.AddHours(8); // pure Date-T-00:00 should be converted to UtcTime with Time component. Assume morning as 8:00.
            else
                _startDateTimeUtc = _forcedStartDateTimeUtc;

            Log($"EarliestCashDay: {earliestCashDateOnlyUtc: yyyy-MM-dd}, PV startDate: {_startDateTimeUtc: yyyy-MM-dd}");

            // endDate determination
            if (_forcedEndDateTimeUtc == DateTime.MaxValue)
                _endDateTimeUtc = DateTime.UtcNow;
            else
                _endDateTimeUtc = _forcedEndDateTimeUtc;

            if (_endDateTimeUtc < _startDateTimeUtc)
            {
                string errMsg = $"StartDate ({_startDateTimeUtc:yyyy-MM-dd}) should be earlier then EndDate  ({_endDateTimeUtc:yyyy-MM-dd}).";
                Log(errMsg);
                throw new ArgumentOutOfRangeException(errMsg);
            }

            SetStartDate(_startDateTimeUtc.ConvertFromUtc(TimeZone)); // QC SetEndDate(), SetStartDate() expects time to be Local time in the exchange time zone, not UTC.
            SetEndDate(_endDateTimeUtc.ConvertFromUtc(TimeZone));  // QC SetEndDate(), SetStartDate() expects time to be Local time in the exchange time zone, not UTC.
            SetWarmUp(_warmUp);
            SetCash(_initialCash);
            Portfolio.MarginCallModel = MarginCallModel.Null;

            // Add securities and set fees
            _tickers = new HashSet<string>();
            foreach (Trade trade in this.PortTradeHist)
            {
                if (trade.AssetType == AssetType.Option) // TEMP: disable options for now as "TMF 231215C00064000" causes exceptions
                    continue;
                if (trade.Action == TradeAction.Unknown)
                    throw new SqException("Error. Unknown TradeAction.");
                if (trade.Action == TradeAction.Deposit || trade.Action == TradeAction.Withdrawal)
                    continue;

                if (trade.Action == TradeAction.Exercise || trade.Action == TradeAction.Expired)
                    throw new NotImplementedException("Implement option trades later.");

                // if we are here, trade.Action == TradeAction.Buy or Sell (Stock or Option)
                string? symbol = trade.Symbol;
                if (!_tickers.Contains(symbol))
                {
                    AddEquity(symbol, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw);
                    _tickers.Add(symbol);
                    Securities[symbol].FeeModel = new ConstantFeeModel(0);
                    Securities[symbol].SetBuyingPowerModel(new SecurityMarginModel(100m));
                }
            }
        }

        public override void OnData(Slice slice)
        {
            decimal pvBefore = Portfolio.TotalPortfolioValue;
            // if(_yestPV / pvBefore < 0.9m || _yestPV / pvBefore > 1.1m)
            //     Log($"Significant overnight PV change: {slice.Time.Date}. PrevClosePV: {_yestPV}, PV now: {pvBefore}.");
            // Adjust cash based on cash transactions
            while (_cashDateUtcs.Count > 0 && _cashDateUtcs[0] <= slice.Time.Date)
            {
                DateTime cashTradeDate = _cashDateUtcs[0];
                if (_cashTransactionsByDateOnly.TryGetValue(cashTradeDate, out List<Trade> cashTransactions))
                {
                    foreach (Trade cashTrade in cashTransactions)
                    {
                        if (cashTrade.Action == TradeAction.Deposit)
                        {
                            Portfolio.CashBook[cashTrade.Symbol].AddAmount((decimal)cashTrade.Price); // Increase cash for deposits
                            Portfolio.AllRollingDeposits[cashTrade.Symbol].AddAmount((decimal)cashTrade.Price);
                        }
                        else if (cashTrade.Action == TradeAction.Withdrawal)
                        {
                            Portfolio.CashBook[cashTrade.Symbol].AddAmount(-(decimal)cashTrade.Price); // Decrease cash for withdrawals
                            Portfolio.AllRollingDeposits[cashTrade.Symbol].AddAmount(-(decimal)cashTrade.Price);
                        }
                    }
                    _cashDateUtcs.RemoveAt(0);
                    _cashTransactionsByDateOnly.Remove(cashTradeDate);
                }
            }

            // Process trades as their dates come due
            while (_tradeDateUtcs.Count > 0 && _tradeDateUtcs[0] <= slice.Time.Date)
            {
                DateTime tradeDate = _tradeDateUtcs[0];
                List<Trade> trades = _tradesByDateOnly[tradeDate];
                foreach (Trade trade in trades)
                {
                    string ticker = trade.Symbol;
                    int signedQuantity = trade.Action == TradeAction.Sell ? -trade.Quantity : trade.Quantity; // In the database, we only store positive quantities, even for Sell transactions. Here we need to convert them to their negative equivalent. 
                    decimal tradePrice = (decimal)trade.Price;
                    _lastOrderTicket = FixPriceOrder(ticker, signedQuantity, tradePrice);
                }
                _tradeDateUtcs.RemoveAt(0);
                _tradesByDateOnly.Remove(tradeDate);
            }
            decimal pvAfter = Portfolio.TotalPortfolioValue;
            // if(pvAfter / pvBefore < 0.9m || pvAfter / pvBefore > 1.1m)
            //     Log($"Significant intraday PV change: {slice.Time.Date}. ClosePV: {pvAfter}, PV open: {pvBefore}.");
            decimal cash = Portfolio.Cash;

            _portfolioValues.Add((slice.Time.Date, pvBefore, pvAfter, cash));
            _yestPV = pvAfter;
        }
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status == OrderStatus.Filled)
            {
                Order order = Transactions.GetOrderById(orderEvent.OrderId);
                Trade trade = new Trade
                {
                    Id = orderEvent.OrderId,
                    Time = Time,
                    Action = order.Quantity > 0 ? TradeAction.Buy : TradeAction.Sell,
                    Symbol = order.Symbol.Value,
                    Quantity = (int)order.Quantity,
                    Price = (float)orderEvent.FillPrice
                };

                // _executedTrades.Add(trade);
            }
        }

        public override void OnEndOfAlgorithm()
        {
            // WriteTradesToCsv(_tradesByDate, "D:\\Temp\\all_trades.csv");
            // WriteExecutedTradesToCsv(_executedTrades, "D:\\Temp\\executed_trades.csv");
            // WritePortfolioValuesToCsv(_portfolioValues, "D:\\Temp\\portfolio_values.csv");
        }

        // private void WriteTradesToCsv(Dictionary<DateTime, List<Trade>> tradesByDate, string fileName)
        // {
        //     StringBuilder csv = new StringBuilder();
        //     csv.AppendLine("Date,Symbol,Action,Quantity,Price");

        //     foreach (KeyValuePair<DateTime, List<Trade>> entry in tradesByDate)
        //     {
        //         DateTime date = entry.Key;
        //         List<Trade> trades = entry.Value;

        //         foreach (Trade trade in trades)
        //             csv.AppendLine($"{date},{trade.Symbol},{trade.Action},{trade.Quantity},{trade.Price}");
        //     }

        //     File.WriteAllText(fileName, csv.ToString());
        // }

        // private void WriteExecutedTradesToCsv(List<Trade> executedTrades, string fileName)
        // {
        //     StringBuilder csv = new StringBuilder();
        //     csv.AppendLine("Id,Time,Action,Symbol,Quantity,Price");

        //     foreach (Trade trade in executedTrades)
        //         csv.AppendLine($"{trade.Id},{trade.Time},{trade.Action},{trade.Symbol},{trade.Quantity},{trade.Price}");

        //     File.WriteAllText(fileName, csv.ToString());
        // }
        private void WritePortfolioValuesToCsv(List<(DateTime date, decimal pvBefore, decimal pvAfter, decimal cash)> portfolioValues, string fileName)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Date,pvBefore,pvAfter");

            foreach ((DateTime date, decimal pvBefore, decimal pvAfter, decimal cash) entry in portfolioValues)
            {
                string date = entry.date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                csv.AppendLine($"{date},{entry.pvBefore},{entry.pvAfter},{entry.cash}");
            }

            File.WriteAllText(fileName, csv.ToString());
        }
    }
}