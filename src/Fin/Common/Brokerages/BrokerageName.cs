namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Specifices what transaction model and submit/execution rules to use
    /// </summary>
    public enum BrokerageName
    {
        /// <summary>
        /// Transaction and submit/execution rules will be the default as initialized
        /// </summary>
        Default,

        /// <summary>
        /// Transaction and submit/execution rules will be the default as initialized
        /// Alternate naming for default brokerage
        /// </summary>
        QuantConnectBrokerage = Default,

        /// <summary>
        /// Transaction and submit/execution rules will use interactive brokers models
        /// </summary>
        InteractiveBrokersBrokerage,

        /// <summary>
        /// Transaction and submit/execution rules will use tradier models
        /// </summary>
        TradierBrokerage,

        /// <summary>
        /// Transaction and submit/execution rules will use oanda models
        /// </summary>
        OandaBrokerage,

        /// <summary>
        /// Transaction and submit/execution rules will use fxcm models
        /// </summary>
        FxcmBrokerage,

        /// <summary>
        /// Transaction and submit/execution rules will use bitfinex models
        /// </summary>
        Bitfinex,

        /// <summary>
        /// Transaction and submit/execution rules will use binance models
        /// </summary>
        Binance,

        /// <summary>
        /// Transaction and submit/execution rules will use gdax models
        /// </summary>
        GDAX = 12,

        /// <summary>
        /// Transaction and submit/execution rules will use alpaca models
        /// </summary>
        Alpaca,

        /// <summary>
        /// Transaction and submit/execution rules will use AlphaStream models
        /// </summary>
        AlphaStreams,

        /// <summary>
        /// Transaction and submit/execution rules will use Zerodha models
        /// </summary>
        Zerodha,

        /// <summary>
        /// Transaction and submit/execution rules will use Samco models
        /// </summary>
        Samco,

        /// <summary>
        /// Transaction and submit/execution rules will use atreyu models
        /// </summary>
        Atreyu,

        /// <summary>
        /// Transaction and submit/execution rules will use TradingTechnologies models
        /// </summary>
        TradingTechnologies,

        /// <summary>
        /// Transaction and submit/execution rules will use Kraken models
        /// </summary>
        Kraken,

        /// <summary>
        /// Transaction and submit/execution rules will use ftx models
        /// </summary>
        FTX,

        /// <summary>
        /// Transaction and submit/execution rules will use ftx us models
        /// </summary>
        FTXUS,

        /// <summary>
        /// Transaction and submit/execution rules will use Exante models
        /// </summary>
        Exante,

        /// <summary>
        /// Transaction and submit/execution rules will use Binance.US models
        /// </summary>
        BinanceUS,

        /// <summary>
        /// Transaction and submit/execution rules will use Wolverine models
        /// </summary>
        Wolverine,
    }
}
