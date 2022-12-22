using QuantConnect.Algorithm.Framework.Portfolio;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Algorithm framework model that executes portfolio targets
    /// </summary>
    public interface IExecutionModel : INotifiedSecurityChanges
    {
        /// <summary>
        /// Submit orders for the specified portfolio targets.
        /// This model is free to delay or spread out these orders as it sees fit
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The portfolio targets just emitted by the portfolio construction model.
        /// These are always just the new/updated targets and not a complete set of targets</param>
        void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets);
    }
}
