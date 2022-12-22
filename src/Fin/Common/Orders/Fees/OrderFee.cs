using ProtoBuf;
using QuantConnect.Securities;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Defines the result for <see cref="IFeeModel.GetOrderFee"/>
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class OrderFee
    {
        /// <summary>
        /// Gets the order fee
        /// </summary>
        [ProtoMember(1)]
        public CashAmount Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderFee"/> class
        /// </summary>
        /// <param name="orderFee">The order fee</param>
        public OrderFee(CashAmount orderFee)
        {
            Value = new CashAmount(
                orderFee.Amount.Normalize(),
                orderFee.Currency);
        }

        /// <summary>
        /// Applies the order fee to the given portfolio
        /// </summary>
        /// <param name="portfolio">The portfolio instance</param>
        /// <param name="fill">The order fill event</param>
        public virtual void ApplyToPortfolio(SecurityPortfolioManager portfolio, OrderEvent fill)
        {
            portfolio.CashBook[Value.Currency].AddAmount(-Value.Amount);
        }

        /// <summary>
        /// This is for backward compatibility with old 'decimal' order fee
        /// </summary>
        public override string ToString()
        {
            return $"{Value.Amount} {Value.Currency}";
        }

        /// <summary>
        /// This is for backward compatibility with old 'decimal' order fee
        /// </summary>
        public static implicit operator decimal(OrderFee m)
        {
            return m.Value.Amount;
        }

        /// <summary>
        /// Gets an instance of <see cref="OrderFee"/> that represents zero.
        /// </summary>
        public static readonly OrderFee Zero =
            new OrderFee(new CashAmount(0, Currencies.NullCurrency));
    }
}
