using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Fin.Base;

public enum TradeAction : byte
{
    Unknown = 0,
    Deposit = 1,
    Withdrawal = 2,
    Buy = 3,
    Sell = 4,
    Exercise = 5,
    Expired = 6
}

[DebuggerDisplay("{Id}, Action:{Action}, Symbol:{Symbol}, Quantity:{Quantity}, Price:{Price}, Time:{Time}")]
public class Trade
{
    public int Id { get; set; } = -1;
    public DateTime Time { get; set; } = DateTime.MinValue;
    public TradeAction Action { get; set; } = TradeAction.Unknown;
    public AssetType AssetType { get; set; } = AssetType.Unknown;
    public string? Symbol { get; set; } = null; // "QQQ 20241220C494.78"
    public string? UnderlyingSymbol { get; set; } = null; // "QQQ"
    public int Quantity { get; set; } = 0;
    public float Price { get; set; } = 0;
    public CurrencyId Currency { get; set; } = CurrencyId.Unknown;
    public float Commission { get; set; } = 0;
    public ExchangeId ExchangeId { get; set; } = ExchangeId.Unknown;
    public List<int>? ConnectedTrades { get; set; } = null;
    public string? Note { get; set; } = null;

    public Trade()
    {
    }

    public Trade(List<Trade> p_tradesForAutoId)
    {
        // keep the newId calculation logic here, just right before the Trade creation.
        int maxId = -1; // if empty list, newId will be 0, which is OK
        foreach (Trade trade in p_tradesForAutoId)
        {
            if (maxId < trade.Id)
                maxId = trade.Id;
        }
        int newId = maxId + 1;

        Id = newId;
    }

    public Trade(List<Trade> p_tradesForAutoId, DateTime p_time, TradeAction p_action, AssetType p_assetType, string? p_symbol, string? p_undSymbol, int p_quantity, float p_price, CurrencyId p_currency, float p_commission, ExchangeId p_exchangeId, string? p_connectedTrades, string? p_note)
    {
        // keep the newId calculation logic here, just right before the Trade creation.
        int maxId = -1; // if empty list, newId will be 0, which is OK
        foreach (Trade trade in p_tradesForAutoId)
        {
            if (maxId < trade.Id)
                maxId = trade.Id;
        }
        int newId = maxId + 1;

        Id = newId;
        Time = p_time;
        Action = p_action;
        AssetType = p_assetType;
        Symbol = p_symbol;
        UnderlyingSymbol = p_undSymbol;
        Quantity = p_quantity;
        Price = p_price;
        Currency = p_currency;
        Commission = p_commission;
        ExchangeId = p_exchangeId;

        if (p_connectedTrades != null)
        {
            string[] tradeIdsStr = p_connectedTrades.Split(',');
            ConnectedTrades = new List<int>(tradeIdsStr.Length);
            foreach (string tradeIdStr in tradeIdsStr)
            {
                if (int.TryParse(tradeIdStr, out int tradeId))
                    ConnectedTrades.Add(tradeId);
            }
        }

        Note = p_note;
    }
    public Trade(int p_id, DateTime p_time, TradeAction p_action, AssetType p_assetType, string? p_symbol, string? p_undSymbol, int p_quantity, float p_price, CurrencyId p_currency, float p_commission, ExchangeId p_exchangeId, string? p_connectedTrades, string? p_note)
    {
        Id = p_id;
        Time = p_time;
        Action = p_action;
        AssetType = p_assetType;
        Symbol = p_symbol;
        UnderlyingSymbol = p_undSymbol;
        Quantity = p_quantity;
        Price = p_price;
        Currency = p_currency;
        Commission = p_commission;
        ExchangeId = p_exchangeId;

        if (p_connectedTrades != null)
        {
            string[] tradeIdsStr = p_connectedTrades.Split(',');
            ConnectedTrades = new List<int>(tradeIdsStr.Length);
            foreach (string tradeIdStr in tradeIdsStr)
            {
                if (int.TryParse(tradeIdStr, out int tradeId))
                    ConnectedTrades.Add(tradeId);
            }
        }

        Note = p_note;
    }
}

public class TradeComparer : IComparer<Trade> // used in List.BinarySeach()
{
    public int Compare(Trade? x, Trade? y)
    {
        if (x!.Time > y!.Time)
            return 1;
        else if (x!.Time < y!.Time)
            return -1;
        else
            return 0;
    }
}