using Python.Runtime;
using QuantConnect.Securities;

namespace QuantConnect.Python
{
    /// <summary>
    /// Wraps a <see cref="PyObject"/> object that represents a security's model of buying power
    /// </summary>
    public class BuyingPowerModelPythonWrapper : IBuyingPowerModel
    {
        private readonly dynamic _model;

        /// <summary>
        /// Constructor for initializing the <see cref="BuyingPowerModelPythonWrapper"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model">Represents a security's model of buying power</param>
        public BuyingPowerModelPythonWrapper(PyObject model)
        {
            _model = model.ValidateImplementationOf<IBuyingPowerModel>();
        }

        /// <summary>
        /// Gets the buying power available for a trade
        /// </summary>
        /// <param name="parameters">A parameters object containing the algorithm's potrfolio, security, and order direction</param>
        /// <returns>The buying power available for the trade</returns>
        public BuyingPower GetBuyingPower(BuyingPowerParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.GetBuyingPower(parameters) as PyObject).GetAndDispose<BuyingPower>();
            }
        }

        /// <summary>
        /// Gets the current leverage of the security
        /// </summary>
        /// <param name="security">The security to get leverage for</param>
        /// <returns>The current leverage in the security</returns>
        public decimal GetLeverage(Security security)
        {
            using (Py.GIL())
            {
                return (_model.GetLeverage(security) as PyObject).GetAndDispose<decimal>();
            }
        }

        /// <summary>
        /// Get the maximum market order quantity to obtain a position with a given buying power percentage.
        /// Will not take into account free buying power.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the target signed buying power percentage</param>
        /// <returns>Returns the maximum allowed market order quantity and if zero, also the reason</returns>
        public GetMaximumOrderQuantityResult GetMaximumOrderQuantityForTargetBuyingPower(GetMaximumOrderQuantityForTargetBuyingPowerParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.GetMaximumOrderQuantityForTargetBuyingPower(parameters)
                    as PyObject).GetAndDispose<GetMaximumOrderQuantityResult>();
            }
        }

        /// <summary>
        /// Get the maximum market order quantity to obtain a delta in the buying power used by a security.
        /// The deltas sign defines the position side to apply it to, positive long, negative short.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the delta buying power</param>
        /// <returns>Returns the maximum allowed market order quantity and if zero, also the reason</returns>
        public GetMaximumOrderQuantityResult GetMaximumOrderQuantityForDeltaBuyingPower(
            GetMaximumOrderQuantityForDeltaBuyingPowerParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.GetMaximumOrderQuantityForDeltaBuyingPower(parameters)
                    as PyObject).GetAndDispose<GetMaximumOrderQuantityResult>();
            }
        }

        /// <summary>
        /// Gets the amount of buying power reserved to maintain the specified position
        /// </summary>
        /// <param name="parameters">A parameters object containing the security</param>
        /// <returns>The reserved buying power in account currency</returns>
        public ReservedBuyingPowerForPosition GetReservedBuyingPowerForPosition(ReservedBuyingPowerForPositionParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.GetReservedBuyingPowerForPosition(parameters)
                    as PyObject).GetAndDispose<ReservedBuyingPowerForPosition>();
            }
        }

        /// <summary>
        /// Check if there is sufficient buying power to execute this order.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>Returns buying power information for an order</returns>
        public HasSufficientBuyingPowerForOrderResult HasSufficientBuyingPowerForOrder(HasSufficientBuyingPowerForOrderParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.HasSufficientBuyingPowerForOrder(parameters)
                    as PyObject).GetAndDispose<HasSufficientBuyingPowerForOrderResult>();
            }
        }

        /// <summary>
        /// Sets the leverage for the applicable securities, i.e, equities
        /// </summary>
        /// <remarks>
        /// This is added to maintain backwards compatibility with the old margin/leverage system
        /// </remarks>
        /// <param name="security">The security to set leverage for</param>
        /// <param name="leverage">The new leverage</param>
        public void SetLeverage(Security security, decimal leverage)
        {
            using (Py.GIL())
            {
                _model.SetLeverage(security, leverage);
            }
        }

        /// <summary>
        /// Gets the margin currently allocated to the specified holding
        /// </summary>
        /// <param name="parameters">An object containing the security</param>
        /// <returns>The maintenance margin required for the provided holdings quantity/cost/value</returns>
        public MaintenanceMargin GetMaintenanceMargin(MaintenanceMarginParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.GetMaintenanceMargin(parameters) as PyObject)
                    .GetAndDispose<MaintenanceMargin>();
            }
        }

        /// <summary>
        /// The margin that must be held in order to increase the position by the provided quantity
        /// </summary>
        /// <param name="parameters">An object containing the security and quantity</param>
        /// <returns>The initial margin required for the provided security and quantity</returns>
        public InitialMargin GetInitialMarginRequirement(InitialMarginParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.GetInitialMarginRequirement(parameters) as PyObject)
                    .GetAndDispose<InitialMargin>();
            }
        }

        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>The total margin in terms of the currency quoted in the order</returns>
        public InitialMargin GetInitialMarginRequiredForOrder(InitialMarginRequiredForOrderParameters parameters)
        {
            using (Py.GIL())
            {
                return (_model.GetInitialMarginRequiredForOrder(parameters) as PyObject)
                    .GetAndDispose<InitialMargin>();
            }
        }
    }
}
