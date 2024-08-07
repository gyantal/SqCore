﻿using System;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators.CandlestickPatterns
{
    /// <summary>
    /// Kicking candlestick pattern
    /// </summary>
    /// <remarks>
    /// Must have:
    /// - first candle: marubozu
    /// - second candle: opposite color marubozu
    /// - gap between the two candles: upside gap if black then white, downside gap if white then black
    /// The meaning of "long body" and "very short shadow" is specified with SetCandleSettings
    /// The returned value is positive(+1) when bullish or negative(-1) when bearish
    /// </remarks>
    public class Kicking : CandlestickPattern
    {
        private readonly int _shadowVeryShortAveragePeriod;
        private readonly int _bodyLongAveragePeriod;

        private decimal[] _shadowVeryShortPeriodTotal = new decimal[2];
        private decimal[] _bodyLongPeriodTotal = new decimal[2];

        /// <summary>
        /// Initializes a new instance of the <see cref="Kicking"/> class using the specified name.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        public Kicking(string name) 
            : base(name, Math.Max(CandleSettings.Get(CandleSettingType.ShadowVeryShort).AveragePeriod, CandleSettings.Get(CandleSettingType.BodyLong).AveragePeriod) + 1 + 1)
        {
            _shadowVeryShortAveragePeriod = CandleSettings.Get(CandleSettingType.ShadowVeryShort).AveragePeriod;
            _bodyLongAveragePeriod = CandleSettings.Get(CandleSettingType.BodyLong).AveragePeriod;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Kicking"/> class.
        /// </summary>
        public Kicking()
            : this("KICKING")
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady
        {
            get { return Samples >= Period; }
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IBaseDataBar> window, IBaseDataBar input)
        {
            if (!IsReady)
            {
                if (Samples >= Period - _shadowVeryShortAveragePeriod)
                {
                    _shadowVeryShortPeriodTotal[1] += GetCandleRange(CandleSettingType.ShadowVeryShort, window[1]);
                    _shadowVeryShortPeriodTotal[0] += GetCandleRange(CandleSettingType.ShadowVeryShort, input);
                }

                if (Samples >= Period - _bodyLongAveragePeriod)
                {
                    _bodyLongPeriodTotal[1] += GetCandleRange(CandleSettingType.BodyLong, window[1]);
                    _bodyLongPeriodTotal[0] += GetCandleRange(CandleSettingType.BodyLong, input);
                }

                return 0m;
            }

            decimal value;
            if (
                // opposite candles
                (int)GetCandleColor(window[1]) == -(int)GetCandleColor(input) &&
                // 1st marubozu
                GetRealBody(window[1]) > GetCandleAverage(CandleSettingType.BodyLong, _bodyLongPeriodTotal[1], window[1]) &&
                GetUpperShadow(window[1]) < GetCandleAverage(CandleSettingType.ShadowVeryShort, _shadowVeryShortPeriodTotal[1], window[1]) &&
                GetLowerShadow(window[1]) < GetCandleAverage(CandleSettingType.ShadowVeryShort, _shadowVeryShortPeriodTotal[1], window[1]) &&
                // 2nd marubozu
                GetRealBody(input) > GetCandleAverage(CandleSettingType.BodyLong, _bodyLongPeriodTotal[0], input) &&
                GetUpperShadow(input) < GetCandleAverage(CandleSettingType.ShadowVeryShort, _shadowVeryShortPeriodTotal[0], input) &&
                GetLowerShadow(input) < GetCandleAverage(CandleSettingType.ShadowVeryShort, _shadowVeryShortPeriodTotal[0], input) &&
                // gap
                (
                  (GetCandleColor(window[1]) == CandleColor.Black && GetCandleGapUp(input, window[1]))
                  ||
                  (GetCandleColor(window[1]) == CandleColor.White && GetCandleGapDown(input, window[1]))
                )
              )
                value = (int)GetCandleColor(input);
            else
                value = 0m;

            // add the current range and subtract the first range: this is done after the pattern recognition 
            // when avgPeriod is not 0, that means "compare with the previous candles" (it excludes the current candle)

            for (var i = 1; i >= 0; i--)
            {
                _shadowVeryShortPeriodTotal[i] += GetCandleRange(CandleSettingType.ShadowVeryShort, window[i]) -
                                                  GetCandleRange(CandleSettingType.ShadowVeryShort, window[i + _shadowVeryShortAveragePeriod]);

                _bodyLongPeriodTotal[i] += GetCandleRange(CandleSettingType.BodyLong, window[i]) -
                                           GetCandleRange(CandleSettingType.BodyLong, window[i + _bodyLongAveragePeriod]);
            }

            return value;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _shadowVeryShortPeriodTotal = new decimal[2];
            _bodyLongPeriodTotal = new decimal[2];
            base.Reset();
        }
    }
}
