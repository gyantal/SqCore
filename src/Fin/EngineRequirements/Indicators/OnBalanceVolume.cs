using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the On Balance Volume (OBV). 
    /// The On Balance Volume is calculated by determining the price of the current close price and previous close price.
    /// If the current close price is equivalent to the previous price the OBV remains the same,
    /// If the current close price is higher the volume of that day is added to the OBV, while a lower close price will
    /// result in negative value.
    /// </summary>
    public class OnBalanceVolume : TradeBarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private TradeBar _previousInput;

        /// <summary>
        /// Initializes a new instance of the Indicator class using the specified name.
        /// </summary> 
        public OnBalanceVolume()
            : base("OBV")
        {
        }

        /// <summary>
        /// Initializes a new instance of the Indicator class using the specified name.
        /// </summary> 
        /// <param name="name">The name of this indicator</param>
        public OnBalanceVolume(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => _previousInput != null;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => 1;

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns> A new value for this indicator </returns>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            var obv = Current.Value;

            if (_previousInput != null)
            {
                if (input.Value > _previousInput.Value)
                {
                    obv += input.Volume;
                    Update(input);
                }
                else if (input.Value < _previousInput.Value)
                {
                    obv -= input.Volume;
                    Update(input);
                }
            }
            else
            {
                obv = input.Volume;
                Update(input);
            }

            _previousInput = input;
            return obv;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _previousInput = null;
            base.Reset();
        }
    }
}