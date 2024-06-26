﻿namespace QuantConnect.Indicators.CandlestickPatterns
{
    /// <summary>
    /// Types of candlestick settings
    /// </summary>
    public enum CandleSettingType
    {
        /// <summary>
        /// Real body is long when it's longer than the average of the 10 previous candles' real body (0)
        /// </summary>
        BodyLong,

        /// <summary>
        /// Real body is very long when it's longer than 3 times the average of the 10 previous candles' real body (1)
        /// </summary>
        BodyVeryLong,

        /// <summary>
        /// Real body is short when it's shorter than the average of the 10 previous candles' real bodies (2)
        /// </summary>
        BodyShort,

        /// <summary>
        /// Real body is like doji's body when it's shorter than 10% the average of the 10 previous candles' high-low range (3)
        /// </summary>
        BodyDoji,

        /// <summary>
        /// Shadow is long when it's longer than the real body (4)
        /// </summary>
        ShadowLong,

        /// <summary>
        /// Shadow is very long when it's longer than 2 times the real body (5)
        /// </summary>
        ShadowVeryLong,

        /// <summary>
        /// Shadow is short when it's shorter than half the average of the 10 previous candles' sum of shadows (6)
        /// </summary>
        ShadowShort,

        /// <summary>
        /// Shadow is very short when it's shorter than 10% the average of the 10 previous candles' high-low range (7)
        /// </summary>
        ShadowVeryShort,

        /// <summary>
        /// When measuring distance between parts of candles or width of gaps
        /// "near" means "&lt;= 20% of the average of the 5 previous candles' high-low range" (8)
        /// </summary>
        Near,

        /// <summary>
        /// When measuring distance between parts of candles or width of gaps
        /// "far" means "&gt;= 60% of the average of the 5 previous candles' high-low range" (9)
        /// </summary>
        Far,

        /// <summary>
        /// When measuring distance between parts of candles or width of gaps
        /// "equal" means "&lt;= 5% of the average of the 5 previous candles' high-low range" (10)
        /// </summary>
        Equal
    }

    /// <summary>
    /// Types of candlestick ranges
    /// </summary>
    public enum CandleRangeType
    {
        /// <summary>
        /// The part of the candle between open and close (0)
        /// </summary>
        RealBody,

        /// <summary>
        /// The complete range of the candle (1)
        /// </summary>
        HighLow,

        /// <summary>
        /// The shadows (or tails) of the candle (2)
        /// </summary>
        Shadows
    }

    /// <summary>
    /// Colors of a candle
    /// </summary>
    public enum CandleColor
    {
        /// <summary>
        /// White is an up candle (close higher or equal than open) (1)
        /// </summary>
        White = 1,

        /// <summary>
        /// Black is a down candle (close lower than open) (-1)
        /// </summary>
        Black = -1
    }
}
