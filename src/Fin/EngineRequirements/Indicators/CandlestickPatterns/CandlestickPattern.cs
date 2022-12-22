﻿using System;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators.CandlestickPatterns
{
    /// <summary>
    /// Abstract base class for a candlestick pattern indicator
    /// </summary>
    public abstract class CandlestickPattern : WindowIndicator<IBaseDataBar>
    {
        /// <summary>
        /// Creates a new <see cref="CandlestickPattern"/> with the specified name
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The number of data points to hold in the window</param>
        protected CandlestickPattern(string name, int period) 
            : base(name, period)
        {
        }

        /// <summary>
        /// Returns the candle color of a candle
        /// </summary>
        /// <param name="tradeBar">The input candle</param>
        protected static CandleColor GetCandleColor(IBaseDataBar tradeBar)
        {
            return tradeBar.Close >= tradeBar.Open ? CandleColor.White : CandleColor.Black;
        }

        /// <summary>
        /// Returns the distance between the close and the open of a candle
        /// </summary>
        /// <param name="tradeBar">The input candle</param>
        protected static decimal GetRealBody(IBaseDataBar tradeBar)
        {
            return Math.Abs(tradeBar.Close - tradeBar.Open);
        }

        /// <summary>
        /// Returns the full range of the candle
        /// </summary>
        /// <param name="tradeBar">The input candle</param>
        protected static decimal GetHighLowRange(IBaseDataBar tradeBar)
        {
            return tradeBar.High - tradeBar.Low;
        }

        /// <summary>
        /// Returns the range of a candle
        /// </summary>
        /// <param name="type">The type of setting to use</param>
        /// <param name="tradeBar">The input candle</param>
        protected static decimal GetCandleRange(CandleSettingType type, IBaseDataBar tradeBar)
        {
            switch (CandleSettings.Get(type).RangeType)
            {
                case CandleRangeType.RealBody:
                    return GetRealBody(tradeBar);
                    
                case CandleRangeType.HighLow:
                    return GetHighLowRange(tradeBar);

                case CandleRangeType.Shadows:
                    return GetUpperShadow(tradeBar) + GetLowerShadow(tradeBar);

                default:
                    return 0m;
            }
        }

        /// <summary>
        /// Returns true if the candle is higher than the previous one
        /// </summary>
        protected static bool GetCandleGapUp(IBaseDataBar tradeBar, IBaseDataBar previousBar)
        {
            return tradeBar.Low > previousBar.High;
        }

        /// <summary>
        /// Returns true if the candle is lower than the previous one
        /// </summary>
        protected static bool GetCandleGapDown(IBaseDataBar tradeBar, IBaseDataBar previousBar)
        {
            return tradeBar.High < previousBar.Low;
        }

        /// <summary>
        /// Returns true if the candle is higher than the previous one (with no body overlap)
        /// </summary>
        protected static bool GetRealBodyGapUp(IBaseDataBar tradeBar, IBaseDataBar previousBar)
        {
            return Math.Min(tradeBar.Open, tradeBar.Close) > Math.Max(previousBar.Open, previousBar.Close);
        }

        /// <summary>
        /// Returns true if the candle is lower than the previous one (with no body overlap)
        /// </summary>
        protected static bool GetRealBodyGapDown(IBaseDataBar tradeBar, IBaseDataBar previousBar)
        {
            return Math.Max(tradeBar.Open, tradeBar.Close) < Math.Min(previousBar.Open, previousBar.Close);
        }

        /// <summary>
        /// Returns the range of the candle's lower shadow
        /// </summary>
        /// <param name="tradeBar">The input candle</param>
        protected static decimal GetLowerShadow(IBaseDataBar tradeBar)
        {
            return (tradeBar.Close >= tradeBar.Open ? tradeBar.Open : tradeBar.Close) - tradeBar.Low;
        }

        /// <summary>
        /// Returns the range of the candle's upper shadow
        /// </summary>
        /// <param name="tradeBar">The input candle</param>
        protected static decimal GetUpperShadow(IBaseDataBar tradeBar)
        {
            return tradeBar.High - (tradeBar.Close >= tradeBar.Open ? tradeBar.Close : tradeBar.Open);
        }

        /// <summary>
        /// Returns the average range of the previous candles
        /// </summary>
        /// <param name="type">The type of setting to use</param>
        /// <param name="sum">The sum of the previous candles ranges</param>
        /// <param name="tradeBar">The input candle</param>
        protected static decimal GetCandleAverage(CandleSettingType type, decimal sum, IBaseDataBar tradeBar)
        {
            var defaultSetting = CandleSettings.Get(type);

            return defaultSetting.Factor *
                (defaultSetting.AveragePeriod != 0 ? sum / defaultSetting.AveragePeriod : GetCandleRange(type, tradeBar)) /
                (defaultSetting.RangeType == CandleRangeType.Shadows ? 2.0m : 1.0m);
        }
    }
}
