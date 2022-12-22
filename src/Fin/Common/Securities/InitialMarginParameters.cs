using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Parameters for <see cref="IBuyingPowerModel.GetInitialMarginRequirement"/>
    /// </summary>
    public class InitialMarginParameters
    {
        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the quantity
        /// </summary>
        public decimal Quantity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InitialMarginParameters"/> class
        /// </summary>
        /// <param name="security">The security</param>
        /// <param name="quantity">The quantity</param>
        public InitialMarginParameters(Security security, decimal quantity)
        {
            Security = security;
            Quantity = quantity;
        }

        /// <summary>
        /// Creates a new instance of <see cref="InitialMarginParameters"/> for the security's underlying
        /// </summary>
        public InitialMarginParameters ForUnderlying()
        {
            var derivative = Security as IDerivativeSecurity;
            if (derivative == null)
            {
                throw new InvalidOperationException("ForUnderlying is only invokable for IDerivativeSecurity (Option|Future)");
            }

            return new InitialMarginParameters(derivative.Underlying, Quantity);
        }
    }
}
