﻿using System;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the True Range (TR).
    /// The True Range is the greatest of the following values:
    /// value1 = distance from today's high to today's low.
    /// value2 = distance from yesterday's close to today's high.
    /// value3 = distance from yesterday's close to today's low.
    /// </summary>
    public class TrueRange : BarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private IBaseDataBar _previousInput;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrueRange"/> class using the specified name.
        /// </summary>
        public TrueRange()
            : base("TR")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrueRange"/> class using the specified name.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        public TrueRange(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples > 1;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => 2;

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            if (!IsReady)
            {
                _previousInput = input;
                return 0m;
            }

            var greatest = input.High - input.Low;
            
            var value2 = Math.Abs(_previousInput.Close - input.High);
            if (value2 > greatest)
                greatest = value2;

            var value3 = Math.Abs(_previousInput.Close - input.Low);
            if (value3 > greatest)
                greatest = value3;

            _previousInput = input;

            return greatest;
        }
    }
}