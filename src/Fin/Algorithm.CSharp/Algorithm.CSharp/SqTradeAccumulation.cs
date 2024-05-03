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
        private Dictionary<DateTime, List<Trade>> _tradesByDate; // same as this.PortTradeHist but that is a List, this is a Dict.
        private HashSet<string> _tickers;
         private List<DateTime> _tradeDates;

        // TODO: Warning First Cash deposit should be treated
        // TODO: Successive cash deposits are even bigger problem... probably
        public override void Initialize()
        {
            _startDate = new DateTime(2020, 01, 03);
            _endDate = DateTime.Now;

            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetWarmUp(_warmUp);
            SetCash(100000);

            _tradesByDate = new Dictionary<DateTime, List<Trade>>();
            foreach (var trade in this.PortTradeHist)
            {
                if (!_tradesByDate.ContainsKey(trade.Time.Date))
                    _tradesByDate[trade.Time.Date] = new List<Trade>();
                _tradesByDate[trade.Time.Date].Add(trade);
            }

            _tradeDates = new List<DateTime>(_tradesByDate.Keys);
            _tradeDates.Sort();
            _tickers = new HashSet<string>();
            foreach (Trade? trade in this.PortTradeHist)
            {
                string? symbol = trade.Symbol;
                if (!_tickers.Contains(symbol))
                {
                    AddEquity(symbol, Resolution.Daily); // TODO: check that it needs RAW. Look at the PV chart aronud split.
                    _tickers.Add(symbol);
                    Securities[symbol].FeeModel = new ConstantFeeModel(0);
                }
            }
        }

        public override void OnData(Slice slice)
        {
            while (_tradeDates.Count > 0 && _tradeDates[0] <= slice.Time.Date)
            {
                DateTime tradeDate = _tradeDates[0];
                List<Trade> trades = _tradesByDate[tradeDate];
                foreach (Trade trade in trades)
                {
                    int adjustedQuantity = trade.Action == TradeAction.Sell ? - trade.Quantity : trade.Quantity;
                    _lastOrderTicket = FixPriceOrder(trade.Symbol, adjustedQuantity, (decimal)trade.Price);
                }
                _tradeDates.RemoveAt(0);
                _tradesByDate.Remove(tradeDate);
            }
        }
    }
}