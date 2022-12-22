using Python.Runtime;
using QuantConnect.Data;
using QuantConnect.Securities;
using System;
using System.Collections.Generic;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Volatility;

namespace QuantConnect.Python
{
    /// <summary>
    /// Provides a volatility model that wraps a <see cref="PyObject"/> object that represents a model that computes the volatility of a security
    /// </summary>
    public class VolatilityModelPythonWrapper : BaseVolatilityModel
    {
        private readonly dynamic _model;

        /// <summary>
        /// Constructor for initialising the <see cref="VolatilityModelPythonWrapper"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model"> Represents a model that computes the volatility of a security</param>
        public VolatilityModelPythonWrapper(PyObject model)
        {
            using (Py.GIL())
            {
                foreach (var attributeName in new[] { "Volatility", "Update", "GetHistoryRequirements" })
                {
                    if (!model.HasAttr(attributeName))
                    {
                        throw new NotImplementedException($"IVolatilityModel.{attributeName} must be implemented. Please implement this missing method on {model.GetPythonType()}");
                    }
                }
            }
            _model = model;
        }

        /// <summary>
        /// Gets the volatility of the security as a percentage
        /// </summary>
        public override decimal Volatility
        {
            get
            {
                using (Py.GIL())
                {
                    return (_model.Volatility as PyObject).GetAndDispose<decimal>();
                }
            }
        }

        /// <summary>
        /// Updates this model using the new price information in
        /// the specified security instance
        /// </summary>
        /// <param name="security">The security to calculate volatility for</param>
        /// <param name="data">The new data used to update the model</param>
        public override void Update(Security security, BaseData data)
        {
            using (Py.GIL())
            {
                _model.Update(security, data);
            }
        }

        /// <summary>
        /// Returns history requirements for the volatility model expressed in the form of history request
        /// </summary>
        /// <param name="security">The security of the request</param>
        /// <param name="utcTime">The date/time of the request</param>
        /// <returns>History request object list, or empty if no requirements</returns>
        public override IEnumerable<HistoryRequest> GetHistoryRequirements(Security security, DateTime utcTime)
        {
            using (Py.GIL())
            {
                return (_model.GetHistoryRequirements(security, utcTime) as PyObject).GetAndDispose<IEnumerable<HistoryRequest>>();
            }
        }

        /// <summary>
        /// Sets the <see cref="ISubscriptionDataConfigProvider"/> instance to use.
        /// </summary>
        /// <param name="subscriptionDataConfigProvider">Provides access to registered <see cref="SubscriptionDataConfig"/></param>
        public override void SetSubscriptionDataConfigProvider(
            ISubscriptionDataConfigProvider subscriptionDataConfigProvider)
        {
            using (Py.GIL())
            {
                if (_model.HasAttr("SetSubscriptionDataConfigProvider"))
                {
                    _model.SetSubscriptionDataConfigProvider(subscriptionDataConfigProvider);
                }
            }
        }
    }
}