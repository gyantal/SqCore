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
#endregion
namespace QuantConnect.Algorithm.CSharp
{
    public class SqTradeAccumulation : QCAlgorithm
    {
        DateTime _startDate = DateTime.MinValue;
        DateTime _endDate = DateTime.MaxValue;
        TimeSpan _warmUp = TimeSpan.Zero;
        OrderTicket _lastOrderTicket = null;
        private Dictionary<DateTime, List<Trade>> _tradesByDate; //  Contains only Buy and Sell trades. Same as this.PortTradeHist but that is a List, this is a Dict.
        private Dictionary<DateTime, List<Trade>> _cashTransactionsByDate; // Contains only Deposit and Withdrawal
        private HashSet<string> _tickers; // Set of unique symbols traded
        private List<DateTime> _tradeDates; // Sorted list of trade dates
        private List<DateTime> _cashDates; // Sorted list of cash transaction dates
        // private List<Trade> _executedTrades;
        private decimal _initialCash;
        private decimal _yestPV;
        private List<(DateTime date, decimal pvBefore, decimal pvAfter)> _portfolioValues;

        public override void Initialize()
        {
            _endDate = DateTime.Now;

            _tradesByDate = new Dictionary<DateTime, List<Trade>>();
            _cashTransactionsByDate = new Dictionary<DateTime, List<Trade>>();
            // _executedTrades = new List<Trade>();
            _portfolioValues = new List<(DateTime date, decimal pvBefore, decimal pvAfter)>();

            // Populate the _tradesByDate, _cashTransactionsByDate dictionaries based on the type of transaction
            DateTime prevDate = DateTime.MinValue; // For checking if the trades are sorted by date
            foreach (Fin.Base.Trade? trade in this.PortTradeHist)
            {
                if (trade.Time < prevDate) // We Assume that trades are sotred by date. Hard requirement. If not, we don't process further, forcing the user to change the trades. This is for the purpose of fast execution.
                    throw new Exception("Tradelist (PortfolioTradeHistory) is not sorted by date.");
                prevDate = trade.Time;
                DateTime tradeDate = trade.Time.Date;
                switch (trade.Action)
                {
                    case TradeAction.Buy:
                    case TradeAction.Sell:
                        if (!_tradesByDate.ContainsKey(tradeDate))
                            _tradesByDate[tradeDate] = new List<Trade>();
                        _tradesByDate[tradeDate].Add(trade);
                        break;
                    case TradeAction.Deposit:
                    case TradeAction.Withdrawal:
                        if (!_cashTransactionsByDate.ContainsKey(tradeDate))
                            _cashTransactionsByDate[tradeDate] = new List<Trade>();
                        _cashTransactionsByDate[tradeDate].Add(trade);
                        break;
                    default:
                        continue;
                }
            }

            _tradeDates = new List<DateTime>(_tradesByDate.Keys);
            _cashDates = new List<DateTime>(_cashTransactionsByDate.Keys);

            // Set initial cash and start date based on the earliest cash transaction date
            if (_cashDates.Count > 0)
            {
                DateTime firstCashDate = _cashDates[0];
                List<Trade> firstCashTransactions = _cashTransactionsByDate[firstCashDate];

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

                _startDate = firstCashDate;
                _cashDates.RemoveAt(0);
                _cashTransactionsByDate.Remove(firstCashDate); // Remove the processed date
            }
            else
                throw new Exception("No cash transactions found.");

            // Ensure the start date is not later than the first trade date
            if (_tradeDates.Count > 0 && _startDate > _tradeDates[0])
                _startDate = _tradeDates[0];

            SetStartDate(_startDate.AddDays(-1));
            SetEndDate(_endDate);
            SetWarmUp(_warmUp);
            SetCash(_initialCash);
            Portfolio.MarginCallModel = MarginCallModel.Null;

            // Add securities and set fees
            _tickers = new HashSet<string>();
            foreach (Trade trade in this.PortTradeHist)
            {
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
            while (_cashDates.Count > 0 && _cashDates[0] <= slice.Time.Date)
            {
                DateTime cashTradeDate = _cashDates[0];
                if (_cashTransactionsByDate.TryGetValue(cashTradeDate, out List<Trade> cashTransactions))
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
                    _cashDates.RemoveAt(0);
                    _cashTransactionsByDate.Remove(cashTradeDate);
                }
            }

            // Process trades as their dates come due
            while (_tradeDates.Count > 0 && _tradeDates[0] <= slice.Time.Date)
            {
                DateTime tradeDate = _tradeDates[0];
                List<Trade> trades = _tradesByDate[tradeDate];
                foreach (Trade trade in trades)
                {
                    string ticker = trade.Symbol;
                    int adjustedQuantity = trade.Action == TradeAction.Sell ? -trade.Quantity : trade.Quantity; // In the database, we only store positive quantities, even for Sell transactions. Here we need to convert them to their negative equivalent. 
                    decimal tradePrice = (decimal)trade.Price;
                    decimal splitFactor = 1m;
                    if (slice.Splits.ContainsKey(ticker))
                    {
                        Split split = slice.Splits[ticker];
                        if (split.Type == SplitType.SplitOccurred)
                        {
                            splitFactor = split.SplitFactor;
                            adjustedQuantity = (int)(adjustedQuantity / splitFactor);
                            tradePrice *= splitFactor;
                        }
                    }
                    // if (slice.Dividends.ContainsKey(ticker)) // In the QC system, dividend handling occurs before the slice arrives. Since the FixPrice trade comes later, the dividends need to be applied retroactively to it as well.
                    // {
                    //     Dividend dividend = slice.Dividends[ticker];
                    //     decimal adjustedDividend = dividend.Distribution * splitFactor;
                    //     decimal totalDividendCorrection = adjustedQuantity * dividend.Distribution;
                    //     Portfolio.CashBook["USD"].AddAmount(totalDividendCorrection);
                    // }
                    _lastOrderTicket = FixPriceOrder(ticker, adjustedQuantity, tradePrice);

                    // // TradeBar tradeBar = slice.Bars[ticker];
                    // TradeBar tradeBar;
                    // decimal closePrice;
                    // if (!slice.Bars.TryGetValue(ticker, out tradeBar))
                    // {
                    //     Console.WriteLine($"No tradebar for {ticker} on {tradeDate}.");
                    //     // throw new Exception($"No tradebar for {ticker} on {tradeDate}.");
                    //     closePrice = 100m;
                    // }
                    // else 
                    //     closePrice = tradeBar.Close;
                    
                    // if ((closePrice / tradePrice > 1.1m) || (closePrice / tradePrice < 0.9m))
                    //     Log($"Significant price difference: {ticker}. ClosePrice: {closePrice}, FixPrice: {tradePrice}.");
                }
                _tradeDates.RemoveAt(0);
                _tradesByDate.Remove(tradeDate);
            }
            decimal pvAfter = Portfolio.TotalPortfolioValue;
            // if(pvAfter / pvBefore < 0.9m || pvAfter / pvBefore > 1.1m)
            //     Log($"Significant intraday PV change: {slice.Time.Date}. ClosePV: {pvAfter}, PV open: {pvBefore}.");

            _portfolioValues.Add((slice.Time.Date, pvBefore, pvAfter));
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
            WritePortfolioValuesToCsv(_portfolioValues, "D:\\Temp\\portfolio_values.csv");
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
        private void WritePortfolioValuesToCsv(List<(DateTime date, decimal pvBefore, decimal pvAfter)> portfolioValues, string fileName)
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Date,pvBefore,pvAfter");

            foreach ((DateTime date, decimal pvBefore, decimal pvAfter) entry in portfolioValues)
            {
                string date = entry.date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                csv.AppendLine($"{date},{entry.pvBefore},{entry.pvAfter}");
            }

            File.WriteAllText(fileName, csv.ToString());
        }
    }
}