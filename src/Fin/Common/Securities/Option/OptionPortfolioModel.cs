using QuantConnect.Orders;
using SqCommon;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Provides an implementation of <see cref="ISecurityPortfolioModel"/> for options that supports
    /// default fills as well as option exercising.
    /// </summary>
    public class OptionPortfolioModel : SecurityPortfolioModel
    {
        /// <summary>
        /// Performs application of an OrderEvent to the portfolio
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">Option security</param>
        /// <param name="fill">The order event fill object to be applied</param>
        public override void ProcessFill(SecurityPortfolioManager portfolio, Security security, OrderEvent fill)
        {
            var order = portfolio.Transactions.GetOrderById(fill.OrderId);
            if (order == null)
            {
                Utils.Logger.Error(Invariant($"OptionPortfolioModel.ProcessFill(): Unable to locate Order with id {fill.OrderId}"));
                return;
            }

            if (order.Type == OrderType.OptionExercise)
            {
                ProcessExerciseFill(portfolio, security, order, fill);
            }
            else
            {
                // we delegate the call to the base class (default behavior)
                base.ProcessFill(portfolio, security, fill);
            }
        }

        /// <summary>
        /// Processes exercise/assignment event to the portfolio
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">Option security</param>
        /// <param name="order">The order object to be applied</param>
        /// <param name="fill">The order event fill object to be applied</param>
        public void ProcessExerciseFill(SecurityPortfolioManager portfolio, Security security, Order order, OrderEvent fill)
        {
            var exerciseOrder = (OptionExerciseOrder)order;
            var option = (Option)portfolio.Securities[exerciseOrder.Symbol];
            var underlying = option.Underlying;
            var cashQuote = option.QuoteCurrency;
            var optionQuantity = order.Quantity;
            var processSecurity = portfolio.Securities[fill.Symbol];

            // depending on option settlement terms we either add underlying to the account or add cash equivalent
            // we then remove the exercised contracts from our option position
            switch (option.ExerciseSettlement)
            {
                case SettlementType.PhysicalDelivery:

                    base.ProcessFill(portfolio, processSecurity, fill);
                    break;

                case SettlementType.Cash:

                    var cashQuantity = -option.GetIntrinsicValue(underlying.Close) * option.ContractUnitOfTrade * optionQuantity;

                    // we add cash equivalent to portfolio
                    option.SettlementModel.ApplyFunds(portfolio, option, fill.UtcTime, cashQuote.Symbol, cashQuantity);

                    base.ProcessFill(portfolio, processSecurity, fill);
                    break;
            }
        }
    }
}
