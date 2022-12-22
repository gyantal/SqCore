using QuantConnect.Algorithm.Framework.Portfolio;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Provides an implementation of <see cref="IExecutionModel"/> that does nothing
    /// </summary>
    public class NullExecutionModel : ExecutionModel
    {
        /// <summary>
        /// Execute the ExecutionModel
        /// </summary>
        /// <param name="algorithm">The Algorithm to execute this model on</param>
        /// <param name="targets">The portfolio targets</param>
        public override void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            // NOP
        }
    }
}