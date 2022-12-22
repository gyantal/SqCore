using System;
using QuantConnect.Securities;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Provides an order fee model that always returns the same order fee.
    /// </summary>
    public class ConstantFeeModel : FeeModel
    {
        private readonly decimal _fee;
        private readonly string _currency;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstantFeeModel"/> class with the specified <paramref name="fee"/>
        /// </summary>
        /// <param name="fee">The constant order fee used by the model</param>
        /// <param name="currency">The currency of the order fee</param>
        public ConstantFeeModel(decimal fee, string currency = "USD")
        {
            _fee = Math.Abs(fee);
            _currency = currency;
        }

        /// <summary>
        /// Returns the constant fee for the model in units of the account currency
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in units of the account currency</returns>
        public override OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            return new OrderFee(new CashAmount(_fee, _currency));
        }
    }
}