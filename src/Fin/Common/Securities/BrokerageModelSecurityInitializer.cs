using QuantConnect.Brokerages;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides an implementation of <see cref="ISecurityInitializer"/> that initializes a security
    /// by settings the <see cref="Security.FillModel"/>, <see cref="Security.FeeModel"/>,
    /// <see cref="Security.SlippageModel"/>, and the <see cref="Security.SettlementModel"/> properties
    /// </summary>
    public class BrokerageModelSecurityInitializer : ISecurityInitializer
    {
        private readonly IBrokerageModel _brokerageModel;
        private readonly ISecuritySeeder _securitySeeder;

        public IBrokerageModel BrokerageModel
        {
            get { return _brokerageModel; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrokerageModelSecurityInitializer"/> class
        /// for the specified algorithm
        /// </summary>
        public BrokerageModelSecurityInitializer()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrokerageModelSecurityInitializer"/> class
        /// for the specified algorithm
        /// </summary>
        /// <param name="brokerageModel">The brokerage model used to initialize the security models</param>
        /// <param name="securitySeeder">An <see cref="ISecuritySeeder"/> used to seed the initial price of the security</param>
        public BrokerageModelSecurityInitializer(IBrokerageModel brokerageModel, ISecuritySeeder securitySeeder)
        {
            _brokerageModel = brokerageModel;
            _securitySeeder = securitySeeder;
        }

        /// <summary>
        /// Initializes the specified security by setting up the models
        /// </summary>
        /// <param name="security">The security to be initialized</param>
        public virtual void Initialize(Security security)
        {
            // Sets the security models
            security.FillModel = _brokerageModel.GetFillModel(security);
            security.FeeModel = _brokerageModel.GetFeeModel(security);
            security.SlippageModel = _brokerageModel.GetSlippageModel(security);
            security.SettlementModel = _brokerageModel.GetSettlementModel(security);
            security.BuyingPowerModel = _brokerageModel.GetBuyingPowerModel(security);
            // Sets the leverage after the buying power model. Otherwise we would set the leverage of the default model.
            security.SetLeverage(_brokerageModel.GetLeverage(security));
            security.SetShortableProvider(_brokerageModel.GetShortableProvider());

            _securitySeeder.SeedSecurity(security);
        }
    }
}
