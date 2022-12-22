using System;
using System.Collections.Generic;
using System.Linq;
using Python.Runtime;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Framework.Risk
{
    /// <summary>
    /// Provides an implementation of <see cref="IRiskManagementModel"/> that combines multiple risk
    /// models into a single risk management model and properly sets each insights 'SourceModel' property.
    /// </summary>
    public class CompositeRiskManagementModel : RiskManagementModel
    {
        private readonly List<IRiskManagementModel> _riskManagementModels = new List<IRiskManagementModel>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeRiskManagementModel"/> class
        /// </summary>
        /// <param name="riskManagementModels">The individual risk management models defining this composite model</param>
        public CompositeRiskManagementModel(params IRiskManagementModel[] riskManagementModels)
        {
            if (riskManagementModels.IsNullOrEmpty())
            {
                throw new ArgumentException("Must specify at least 1 risk management model for the CompositeRiskManagementModel");
            }

            _riskManagementModels.AddRange(riskManagementModels);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeRiskManagementModel"/> class
        /// </summary>
        /// <param name="riskManagementModels">The individual risk management models defining this composite model</param>
        public CompositeRiskManagementModel(IEnumerable<IRiskManagementModel>riskManagementModels)
        {
            foreach (var riskManagementModel in riskManagementModels)
            {
                AddRiskManagement(riskManagementModel);
            }

            if (_riskManagementModels.IsNullOrEmpty())
            {
                throw new ArgumentException("Must specify at least 1 risk management model for the CompositeRiskManagementModel");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeRiskManagementModel"/> class
        /// </summary>
        /// <param name="riskManagementModels">The individual risk management models defining this composite model</param>
        public CompositeRiskManagementModel(params PyObject[] riskManagementModels)
        {
            if (riskManagementModels.IsNullOrEmpty())
            {
                throw new ArgumentException("Must specify at least 1 risk management model for the CompositeRiskManagementModel");
            }

            foreach (var pyRiskManagementModel in riskManagementModels)
            {
                AddRiskManagement(pyRiskManagementModel);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeRiskManagementModel"/> class
        /// </summary>
        /// <param name="riskManagementModel">The individual risk management model defining this composite model</param>
        public CompositeRiskManagementModel(PyObject riskManagementModel)
            : this(new[] { riskManagementModel} )
        {

        }

        /// <summary>
        /// Manages the algorithm's risk at each time step.
        /// This method patches this call through the each of the wrapped models.
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The current portfolio targets to be assessed for risk</param>
        /// <returns>The new portfolio targets</returns>
        public override IEnumerable<IPortfolioTarget> ManageRisk(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            foreach (var model in _riskManagementModels)
            {
                // take into account the possibility of ManageRisk returning nothing
                var riskAdjusted = model.ManageRisk(algorithm, targets);

                // produce a distinct set of new targets giving preference to newer targets
                targets = riskAdjusted.Concat(targets).DistinctBy(t => t.Symbol).ToArray();
            }

            return targets;
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed.
        /// This method patches this call through the each of the wrapped models.
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
            foreach (var model in _riskManagementModels)
            {
                model.OnSecuritiesChanged(algorithm, changes);
            }
        }

        /// <summary>
        /// Adds a new <see cref="IRiskManagementModel"/> instance
        /// </summary>
        /// <param name="riskManagementModel">The risk management model to add</param>
        public void AddRiskManagement(IRiskManagementModel riskManagementModel)
        {
            _riskManagementModels.Add(riskManagementModel);
        }

        /// <summary>
        /// Adds a new <see cref="IRiskManagementModel"/> instance
        /// </summary>
        /// <param name="pyRiskManagementModel">The risk management model to add</param>
        public void AddRiskManagement(PyObject pyRiskManagementModel)
        {
            IRiskManagementModel riskManagementModel;
            if (!pyRiskManagementModel.TryConvert(out riskManagementModel))
            {
                riskManagementModel = new RiskManagementModelPythonWrapper(pyRiskManagementModel);
            }
            _riskManagementModels.Add(riskManagementModel);
        }
    }
}
