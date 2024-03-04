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
using Fin.Base;
using StackExchange.Redis;

namespace Fin.MemDb;

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
    public string? Note { get; set; } = null;

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
        UnderlyingSymbol = (AssetType == 'S') ? null : p_trade.UnderlyingSymbol;
        Quantity = p_trade.Quantity;
        Price = p_trade.Price;
        Currency = (p_trade.Currency == CurrencyId.Unknown || p_trade.Currency == CurrencyId.USD) ? null : AssetHelper.gCurrencyToString[p_trade.Currency];
        Commission = p_trade.Commission == 0f ? null : p_trade.Commission;
        Exchange = p_trade.ExchangeId == ExchangeId.Unknown ? null : AssetHelper.gExchangeToStr[p_trade.ExchangeId];
        ConnectedTrades = p_trade.ConnectedTrades != null ? string.Join(",", p_trade.ConnectedTrades) : null;
        Note = p_trade.Note;
    }

    public Trade ToTrade()
    {
        Trade trade = new(Id, DateTime.Parse(Time), AssetHelper.gStrToTradeAction[Action ?? "BOT"], AssetHelper.gChrToAssetType[AssetType ?? 'S'], Symbol, UnderlyingSymbol ?? Symbol, Quantity, Price,
            Currency == null ? CurrencyId.Unknown : AssetHelper.gStrToCurrency[Currency], Commission ?? 0,
            Exchange == null ? ExchangeId.Unknown : AssetHelper.gStrToExchange[Exchange], ConnectedTrades, Note);

        return trade;
    }

    public static RedisValue ToRedisValue(List<Trade> p_trades, bool p_forceChronologicalOrder = true)
    {
        List<Trade> tradesToDb;
        if (p_forceChronologicalOrder)
        {
            // Sort Trade objects by Time
            // tradesToDb = p_trades.OrderBy(t => t.Time).ToList(); // LINQ slow sort into a new memory location

            tradesToDb = p_trades;
            tradesToDb.Sort((a, b) => a.Time.CompareTo(b.Time)); // fast sort it in place

            // Reassign IDs
            Dictionary<int, int> idMap = new ();
            for (int newId = 0; newId < tradesToDb.Count; newId++)
            {
                int oldId = tradesToDb[newId].Id;
                idMap[oldId] = newId;
            }

            // Update IDs in Ids and in ConnectedTrades
            foreach (Trade trade in tradesToDb)
            {
                trade.Id = idMap[trade.Id];
                if (trade.ConnectedTrades != null && trade.ConnectedTrades.Any())
                {
                    List<int> newConnectedTrades = new(trade.ConnectedTrades.Count);
                    foreach (int id in trade.ConnectedTrades)
                        newConnectedTrades.Add(idMap[id]);
                    trade.ConnectedTrades = newConnectedTrades;
                }
            }
        }
        else
            tradesToDb = p_trades;

        // Convert Trade objects to TradeInDb objects
        List<TradeInDb> tradeInDbsToDb = new(tradesToDb.Count);
        foreach (var trade in tradesToDb)
            tradeInDbsToDb.Add(new TradeInDb(trade));

        // Serialize the list of TradeInDb objects to JSON
        return JsonSerializer.Serialize(tradeInDbsToDb, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }
}