using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YahooFinanceApi;

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
    public string GetPctChnData([FromBody] UserInput p_inMsg)
    {
        if (!string.IsNullOrEmpty(p_inMsg.Tickers))
        {
            string[] tickers = p_inMsg.Tickers.Split(',', StringSplitOptions.RemoveEmptyEntries);
            List<Tuple<string, List<Tuple<float, List<Tuple<float, Controllers.PctChnSignal>>>>>> pctChnData = new();
            foreach (string ticker in tickers)
            {
                DateTime endDate = DateTime.UtcNow;
                DateTime startDate = endDate.AddDays(-600);
                IReadOnlyList<Candle?>? history = Yahoo.GetHistoricalAsync(ticker, startDate, endDate, YahooFinanceApi.Period.Daily).Result;
                List<float> adjustedClosePrices = new();
                foreach (var candle in history)
                    adjustedClosePrices.Add((float)candle!.AdjustedClose);

                int bottomPctThreshold = 25;
                int topPctThreshold = 75;
                int[] pctChnLookbackDays = new int[] { 60, 120, 180, 252 };
                int calculationLookbackDays = 50;
                int resultLengthDays = 20;
                List<Tuple<float, List<Tuple<float, Controllers.PctChnSignal>>>> pctChannelRes = Controllers.StrategyUberTaaController.PctChnWeights(adjustedClosePrices, pctChnLookbackDays, calculationLookbackDays, resultLengthDays, bottomPctThreshold, topPctThreshold);
                pctChnData.Add(new Tuple<string, List<Tuple<float, List<Tuple<float, Controllers.PctChnSignal>>>>>(ticker, pctChannelRes));
            }
            string pctChnDataStr = JsonSerializer.Serialize(pctChnData);
            return pctChnDataStr;
        }
        else
            return JsonSerializer.Serialize(new { error = "GetPctChnData: p_inMsg is null or empty." });
    }
}