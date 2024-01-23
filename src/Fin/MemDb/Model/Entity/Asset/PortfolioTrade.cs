// Example TradeHistory in JSON:
// [{"ID":0,"Time":"2023-12-11T21:00:00","Action":"BOT","Symbol":"TSLA","Qnty":"10","Pr":"240.2","Comm":2.2},{"ID":1,"Time":"2023-12-12T21:00:00","Action":"SLD","Symbol":"TSLA","Qnty":"10","Pr":"242.2","Comm":2.1},
// {"ID":2,"Time":"2023-12-13T21:00:00","Action":"BOT","ScrType":"O","Symbol":"TMF 231215C00064000","UndSymbol":"TMF","Qnty":"1","Pr":"2.2","Comm":3.2},
// {"ID":3,"Time":"2023-12-14T21:00:00","Action":"EXC","Symbol":"TMF 231215C00064000","UndSymbol":"TMF","Qnty":"11","ConnTds":"4"},
// {"ID":4,"Time":"2023-12-14T21:00:00","Action":"BOT","Symbol":"TMF","Qnty":"100","Pr":"64"}]

// Schema : ID (use TradeID because Exercise Action refers another sub-trade)/Time/ Action/ScrType (BOT,SLD,EXP (Expired with 0),EXC(Exercised and bought/sold stocks),DPT(deposit),WTD: (withdrawal))
// /Symbol/UnderlyingSymbol/Quantity/Price/Currency/Comission (using Portfolio.BaseCurrency)/ ExchangeID/ConnectedTrades="34,53"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace Fin.MemDb;

public enum TradeActionType : byte
{
    Unknown = 0,
    Deposit = 1,
    Withdrawal = 2,
    Buy = 3,
    Sell = 4,
    Exercise = 5,
    Expired = 6
}

public class TradeInDb
{
    [JsonPropertyName("ID")]
    public int Id { get; set; } = -1;
    public string Time { get; set; } = DateTime.MinValue.ToString();
    public string? Action { get; set; } = null;
    [JsonPropertyName("ScrType")]
    public char? AssetType { get; set; } = null;
    public string? Symbol { get; set; } = null;
    [JsonPropertyName("UndSymbol")]
    public string? UnderlyingSymbol { get; set; } = null;
    [JsonPropertyName("Qnty")]
    public int Quantity { get; set; } = 0;
    [JsonPropertyName("Pr")]
    public float Price { get; set; } = 0;
    [JsonPropertyName("Curr")]
    public string? Currency { get; set; } = null;
    [JsonPropertyName("Comm")]
    public float? Commission { get; set; } = 0;
    [JsonPropertyName("Exch")]
    public string? Exchange { get; set; } = null;
    [JsonPropertyName("ConnTds")]
    public string? ConnectedTrades { get; set; } = null;

    public TradeInDb()
    {
    }

    public TradeInDb(Trade p_trade)
    {
        Id = p_trade.Id;
        Time = p_trade.Time.ToString("yyyy-MM-ddTHH:mm:ss");
        Action = AssetHelper.gTradeActionToStr[p_trade.Action];
        AssetType = AssetHelper.gAssetTypeCode[p_trade.AssetType];
        Symbol = p_trade.Symbol;
        UnderlyingSymbol = p_trade.UnderlyingSymbol;
        Quantity = p_trade.Quantity;
        Price = p_trade.Price;
        Currency = p_trade.Currency == CurrencyId.Unknown ? null : AssetHelper.gCurrencyToString[p_trade.Currency];
        Commission = p_trade.Commission == 0f ? null : p_trade.Commission;
        Exchange = p_trade.ExchangeId == ExchangeId.Unknown ? null : AssetHelper.gExchangeToStr[p_trade.ExchangeId];
        ConnectedTrades = p_trade.ConnectedTrades != null ? string.Join(",", p_trade.ConnectedTrades) : null;
    }

    public static RedisValue ToRedisValue(List<Trade> p_tradeList)
    {
        List<TradeInDb> tradesToDb = new ();
        for (int i = 0; i < p_tradeList.Count; i++)
        {
            Trade? trade = p_tradeList[i];
            TradeInDb tradeInDb = new(trade);
            tradesToDb.Add(tradeInDb);
        }

        var serializeOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(tradesToDb, serializeOptions);
    }
}

[DebuggerDisplay("{Id}, Action:{Action}, Symbol:{Symbol}, Quantity:{Quantity}, Price:{Price}, Time:{Time}")]
public class Trade
{
    public int Id { get; set; } = -1;
    public DateTime Time { get; set; } = DateTime.MinValue;
    public TradeActionType Action { get; set; } = TradeActionType.Unknown;
    public AssetType AssetType { get; set; } = AssetType.Unknown;
    public string? Symbol { get; set; } = null;
    public string? UnderlyingSymbol { get; set; } = null;
    public int Quantity { get; set; } = 0;
    public float Price { get; set; } = 0;
    public CurrencyId Currency { get; set; } = CurrencyId.Unknown;
    public float Commission { get; set; } = 0;
    public ExchangeId ExchangeId { get; set; } = ExchangeId.Unknown;
    public List<int>? ConnectedTrades { get; set; } = null;

    public Trade()
    {
    }

    public Trade(TradeInDb p_tradeInDb)
    {
        Id = p_tradeInDb.Id;
        Time = DateTime.Parse(p_tradeInDb.Time);
        Action = AssetHelper.gStrToTradeAction[p_tradeInDb.Action ?? "BOT"];
        AssetType = AssetHelper.gChrToAssetType[p_tradeInDb.AssetType ?? 'S'];
        Symbol = p_tradeInDb.Symbol;
        UnderlyingSymbol = p_tradeInDb.UnderlyingSymbol ?? Symbol;
        Quantity = p_tradeInDb.Quantity;
        Price = p_tradeInDb.Price;
        Currency = p_tradeInDb.Currency == null ? CurrencyId.Unknown : AssetHelper.gStrToCurrency[p_tradeInDb.Currency];
        Commission = p_tradeInDb.Commission ?? 0;
        ExchangeId = p_tradeInDb.Exchange == null ? ExchangeId.Unknown : AssetHelper.gStrToExchange[p_tradeInDb.Exchange];
        if (p_tradeInDb.ConnectedTrades != null)
        {
            string[] tradeIdsStr = p_tradeInDb.ConnectedTrades.Split(',');
            ConnectedTrades = new(tradeIdsStr.Length);
            foreach (var tradeIdStr in tradeIdsStr)
            {
                ConnectedTrades.Add(int.Parse(tradeIdStr));
            }
        }
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

    public Trade(List<Trade> p_tradesForAutoId, DateTime p_time, TradeActionType p_action, AssetType p_assetType, string? p_symbol, string? p_undSymbol, int p_quantity, float p_price, CurrencyId p_currency, float p_commission, ExchangeId p_exchangeId, string? p_connectedTrades)
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
        ConnectedTrades = p_connectedTrades?.Split(',').Select(int.Parse).ToList();
    }
    public Trade(int p_id, DateTime p_time, TradeActionType p_action, AssetType p_assetType, string? p_symbol, string? p_undSymbol, int p_quantity, float p_price, CurrencyId p_currency, float p_commission, ExchangeId p_exchangeId, string? p_connectedTrades)
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
        ConnectedTrades = p_connectedTrades?.Split(',').Select(int.Parse).ToList();
    }
}