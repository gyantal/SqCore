using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
[ResponseCache(CacheProfileName = "NoCache")]
public class SeekingAlphaHelperController : ControllerBase
{
    public class StockData
    {
        public string Ticker { get; set; } = string.Empty;
        public float SaQuantRating { get; set; }
    }

    #pragma warning disable CA1822 // "Mark members as static". Kestrel Controller methods that is called as an URL has to be instance methods, not static.
    [HttpGet] // only 1 HttpGet attribute should be in the Controller (or you have to specify in it how to resolve)
    public IActionResult Get([FromQuery] string dataSelector)
    {
        if (string.IsNullOrEmpty(dataSelector))
            return BadRequest(new { errorMsg = "dataSelector parameter is required." });
        string result = ExtractDataBySelector(dataSelector); // Process the request based on the dataSelector value
        return Ok(result);
    }

    private string ExtractDataBySelector(string p_dataSelector) // p_dataSelector : topStocks or topAnalysts
    {
        // needs to implement the logic based on the dataSelector.
        if (p_dataSelector == "topStocks")
            return "{\"result\": \"Top Stocks Data\"}"; // return data for top stocks
        else if (p_dataSelector == "topAnalysts")
            return "{\"result\": \"Top Analysts Data\"}"; // return data for top analysts
        else
            return "{\"errorMsg\": \"Unknown dataSelector value.\"}";
    }
}