using Python.Runtime;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Python;
using System.Collections.Generic;
using QuantConnect.Algorithm.Framework.Portfolio;

namespace QuantConnect.Algorithm.Framework.Risk
{
    /// <summary>
    /// Provides an implementation of <see cref="IRiskManagementModel"/> that wraps a <see cref="PyObject"/> object
    /// </summary>
    public class RiskManagementModelPythonWrapper : RiskManagementModel
    {
        private readonly dynamic _model;

        /// <summary>
        /// Constructor for initialising the <see cref="IRiskManagementModel"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model">Model defining how risk is managed</param>
        public RiskManagementModelPythonWrapper(PyObject model)
        {
            _model = model.ValidateImplementationOf<IRiskManagementModel>();
        }

        /// <summary>
        /// Manages the algorithm's risk at each time step
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The current portfolio targets to be assessed for risk</param>
        public override IEnumerable<IPortfolioTarget> ManageRisk(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            using (Py.GIL())
            {
                var riskTargetOverrides = _model.ManageRisk(algorithm, targets) as PyObject;
                var iterator = riskTargetOverrides.GetIterator();
                foreach (PyObject target in iterator)
                {
                    yield return target.GetAndDispose<IPortfolioTarget>();
                }
                iterator.Dispose();
                riskTargetOverrides.Dispose();
            }
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
            using (Py.GIL())
            {
                _model.OnSecuritiesChanged(algorithm, changes);
            }
        }
    }
}
