using Newtonsoft.Json;

namespace QuantConnect.Api
{
    /// <summary>
    /// Backtest Report Response wrapper
    /// </summary>
    public class BacktestReport : RestResponse
    {
        /// <summary>
        /// HTML data of the report with embedded base64 images
        /// </summary>
        [JsonProperty(PropertyName = "report")]
        public string Report { get; set; }
    }
}
