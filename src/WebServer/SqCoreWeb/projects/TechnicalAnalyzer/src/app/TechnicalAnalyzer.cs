using System;
using Microsoft.AspNetCore.Mvc;

namespace SqCoreWeb;

public class TechnicalAnalyzerController : Microsoft.AspNetCore.Mvc.Controller
{
    [HttpGet]
    public string Get() // localhost:5001/TechnicalAnalyzer
    {
        string msg = @"{ ""Response"": ""Response from server""}";
        return msg;
    }

    [HttpPost]
    public string GetPctChnData(string p_inMsg)
    {
        Console.WriteLine($"GetPctChnData {p_inMsg}");
        return "{ \"Field1\": \"GetPctChnData response Test\"}";
    }
}