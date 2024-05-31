using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SqCoreWeb;
public class UserInput
{
    public string? Tickers { get; set; }
}

public class TechnicalAnalyzerController : Microsoft.AspNetCore.Mvc.Controller
{
    [HttpGet]
    public string Get() // localhost:5001/TechnicalAnalyzer
    {
        string msg = @"{ ""Response"": ""Response from server""}";
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
        List<Tuple<string, List<Tuple<DateTime, float, List<Tuple<float, Controllers.PctChnSignal>>>>>> pctChnData = new();
        foreach (string ticker in tickers)
        {
            List<Tuple<DateTime, float, List<Tuple<float, Controllers.PctChnSignal>>>> pctChannelRes = Controllers.StrategyUberTaaController.PctChnWeightsWithDates(ticker, endDate, pctChnLookbackDays, calculationLookbackDays, resultLengthDays, bottomPctThreshold, topPctThreshold);
            pctChnData.Add(new Tuple<string, List<Tuple<DateTime, float, List<Tuple<float, Controllers.PctChnSignal>>>>>(ticker, pctChannelRes));
        }
        string pctChnDataStr = JsonSerializer.Serialize(pctChnData);
        return pctChnDataStr;
    }
}