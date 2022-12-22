using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Provides a base class for execution models
    /// </summary>
    public class ExecutionModel : IExecutionModel
    {
        /// <summary>
        /// Submit orders for the specified portfolio targets.
        /// This model is free to delay or spread out these orders as it sees fit
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The portfolio targets just emitted by the portfolio construction model.
        /// These are always just the new/updated targets and not a complete set of targets</param>
        public virtual void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            throw new System.NotImplementedException("Types deriving from 'ExecutionModel' must implement the 'void Execute(QCAlgorithm, IPortfolioTarget[]) method.");
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public virtual void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
        }
    }
}