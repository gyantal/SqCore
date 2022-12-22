using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Parameters for <see cref="IBuyingPowerModel.GetMaintenanceMargin"/>
    /// </summary>
    public class MaintenanceMarginParameters
    {
        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the quantity of the security
        /// </summary>
        public decimal Quantity { get; }

        /// <summary>
        /// Gets the absolute quantity of the security
        /// </summary>
        public decimal AbsoluteQuantity => Math.Abs(Quantity);

        /// <summary>
        /// Gets the holdings cost of the security
        /// </summary>
        public decimal HoldingsCost { get; }

        /// <summary>
        /// Gets the absolute holdings cost of the security
        /// </summary>
        public decimal AbsoluteHoldingsCost => Math.Abs(HoldingsCost);

        /// <summary>
        /// Gets the holdings value of the security
        /// </summary>
        public decimal HoldingsValue { get; }

        /// <summary>
        /// Gets the absolute holdings value of the security
        /// </summary>
        public decimal AbsoluteHoldingsValue => Math.Abs(HoldingsValue);

        /// <summary>
        /// Initializes a new instance of the <see cref="MaintenanceMarginParameters"/> class
        /// </summary>
        /// <param name="security">The security</param>
        /// <param name="quantity">The quantity</param>
        /// <param name="holdingsCost">The holdings cost</param>
        /// <param name="holdingsValue">The holdings value</param>
        public MaintenanceMarginParameters(
            Security security,
            decimal quantity,
            decimal holdingsCost,
            decimal holdingsValue
            )
        {
            Security = security;
            Quantity = quantity;
            HoldingsCost = holdingsCost;
            HoldingsValue = holdingsValue;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MaintenanceMarginParameters"/> class to compute the maintenance margin
        /// required to support the algorithm's current holdings
        /// </summary>
        public static MaintenanceMarginParameters ForCurrentHoldings(Security security)
        {
            return new MaintenanceMarginParameters(security,
                security.Holdings.Quantity,
                security.Holdings.HoldingsCost,
                security.Holdings.HoldingsValue
            );
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MaintenanceMarginParameters"/> class to compute the maintenance margin
        /// required to support the specified quantity of holdings at current market prices
        /// </summary>
        public static MaintenanceMarginParameters ForQuantityAtCurrentPrice(Security security, decimal quantity)
        {
            var value = security.Holdings.GetQuantityValue(quantity);
            return new MaintenanceMarginParameters(security, quantity, value, value);
        }

        /// <summary>
        /// Creates a new instance of <see cref="MaintenanceMarginParameters"/> for the security's underlying
        /// </summary>
        public MaintenanceMarginParameters ForUnderlying(decimal quantity)
        {
            var derivative = Security as IDerivativeSecurity;
            if (derivative == null)
            {
                throw new InvalidOperationException("ForUnderlying is only invokable for IDerivativeSecurity (Option|Future)");
            }

            return ForQuantityAtCurrentPrice(derivative.Underlying, quantity);
        }
    }
}
