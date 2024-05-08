#region imports
using System;
using System.Collections.Generic;
using QuantConnect.Util;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using Fin.Base;
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
        private decimal _initialCash;

        public override void Initialize()
        {
            _endDate = DateTime.Now;

            _tradesByDate = new Dictionary<DateTime, List<Trade>>();
            _cashTransactionsByDate = new Dictionary<DateTime, List<Trade>>();

            // Populate the _tradesByDate, _cashTransactionsByDate dictionaries based on the type of transaction
            foreach (var trade in this.PortTradeHist)
            {
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
            _tradeDates.Sort();
            _cashDates = new List<DateTime>(_cashTransactionsByDate.Keys);
            _cashDates.Sort();

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

            // Add securities and set fees
            _tickers = new HashSet<string>();
            foreach (Trade trade in this.PortTradeHist)
            {
                string? symbol = trade.Symbol;
                if (!_tickers.Contains(symbol))
                {
                    AddEquity(symbol, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw);
                    _tickers.Add(symbol);
                    Securities[symbol].FeeModel = new ConstantFeeModel(0);
                }
            }
        }

        public override void OnData(Slice slice)
        {
            // Adjust cash based on cash transactions
            while (_cashDates.Count > 0 && _cashDates[0] <= slice.Time.Date)
            {
                DateTime cashTradeDate = _cashDates[0];
                if (_cashTransactionsByDate.TryGetValue(cashTradeDate, out List<Trade> cashTransactions))
                {
                    foreach (Trade cashTrade in cashTransactions)
                    {
                        if (cashTrade.Action == TradeAction.Deposit)
                            Portfolio.CashBook["USD"].AddAmount((decimal)cashTrade.Price); // Increase cash for deposits
                        else if (cashTrade.Action == TradeAction.Withdrawal)
                            Portfolio.CashBook["USD"].AddAmount(-(decimal)cashTrade.Price); // Decrease cash for withdrawals
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
                    int adjustedQuantity = trade.Action == TradeAction.Sell ? - trade.Quantity : trade.Quantity; // In the database, we only store positive quantities, even for Sell transactions. Here we need to convert them to their negative equivalent. 
                    _lastOrderTicket = FixPriceOrder(trade.Symbol, adjustedQuantity, (decimal)trade.Price);
                }
                _tradeDates.RemoveAt(0);
                _tradesByDate.Remove(tradeDate);
            }
        }
    }
}