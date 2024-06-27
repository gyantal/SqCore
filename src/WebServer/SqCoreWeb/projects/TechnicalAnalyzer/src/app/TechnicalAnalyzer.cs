using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SqCoreWeb.Controllers;

namespace SqCoreWeb;
public class UserInput
{
    public string? Tickers { get; set; }
}
public struct TickerAggregatePctlChnlData
{
    [JsonPropertyName("t")]
    public string? Ticker { get; set; }
    [JsonPropertyName("ad")]
    public List<AggregateDatePctlChannel>? AggregateDatePctlChannel { get; set; }
}

public class TechnicalAnalyzerController : Microsoft.AspNetCore.Mvc.Controller
{
    [HttpGet]
    public string Get() // localhost:5001/TechnicalAnalyzer
    {
        string msg = @"{ ""Response"": ""Response from server""}";
        return msg;
    }

    [HttpGet]
    public string Ping() // localhost:5001/TechnicalAnalyzer/Ping or https://sqcore.net/TechnicalAnalyzer/Ping
    {
        string msg = @"{ ""Response"": ""Pong""}";
        return msg;
    }

    [HttpPost]
    public string GetPctChnData([FromBody] UserInput p_inMsg) // e.g. p_inMsg.Tickers: TSLA,AAPL
    {
        if (string.IsNullOrEmpty(p_inMsg.Tickers))
            return JsonSerializer.Serialize(new { error = "GetPctChnData: p_inMsg is null or empty." });

        string[] tickers = p_inMsg.Tickers.Split(',', StringSplitOptions.RemoveEmptyEntries);
        DateTime endDate = DateTime.UtcNow;
        int[] pctChnLookbackDays = new int[] { 60, 120, 180, 252 };
        int calculationLookbackDays = 50;
        int resultLengthDays = 20;
        int bottomPctThreshold = 25;
        int topPctThreshold = 75;
        List<TickerAggregatePctlChnlData> pctChnData = new();
        foreach (string ticker in tickers)
        {
            List<Controllers.AggregateDatePctlChannel> pctChannelRes = Controllers.StrategyUberTaaController.PctChnWeightsWithDates_New(ticker, endDate, pctChnLookbackDays, calculationLookbackDays, resultLengthDays, bottomPctThreshold, topPctThreshold);

            TickerAggregatePctlChnlData tickerAggregatePctlChnlData = new TickerAggregatePctlChnlData { Ticker = ticker, AggregateDatePctlChannel = pctChannelRes };
            pctChnData.Add(tickerAggregatePctlChnlData);
        }
        string pctChnDataStr = JsonSerializer.Serialize<List<TickerAggregatePctlChnlData>>(pctChnData);
        return pctChnDataStr;
    }
}