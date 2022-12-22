namespace QuantConnect.Securities
{
    /// <summary>
    /// Result type for <see cref="IBuyingPowerModel.GetMaintenanceMargin"/>
    /// </summary>
    public class MaintenanceMargin
    {
        /// <summary>
        /// Gets an instance of <see cref="MaintenanceMargin"/> with zero values.
        /// </summary>
        public static MaintenanceMargin Zero { get; } = new MaintenanceMargin(0m);

        /// <summary>
        /// The maintenance margin value in account currency
        /// </summary>
        public decimal Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaintenanceMargin"/> class
        /// </summary>
        /// <param name="value">The maintenance margin</param>
        public MaintenanceMargin(decimal value)
        {
            Value = value;
        }

        /// <summary>
        /// Implicit operator <see cref="MaintenanceMargin"/> -> <see cref="decimal"/>
        /// </summary>
        public static implicit operator decimal(MaintenanceMargin margin)
        {
            return margin.Value;
        }

        /// <summary>
        /// Implicit operator <see cref="decimal"/> -> <see cref="MaintenanceMargin"/>
        /// </summary>
        public static implicit operator MaintenanceMargin(decimal margin)
        {
            return new MaintenanceMargin(margin);
        }
    }
}
