using Python.Runtime;
using QuantConnect.Orders;
using QuantConnect.Orders.Slippage;
using QuantConnect.Securities;

namespace QuantConnect.Python
{
    /// <summary>
    /// Wraps a <see cref="PyObject"/> object that represents a model that simulates market order slippage
    /// </summary>
    public class SlippageModelPythonWrapper : ISlippageModel
    {
        private readonly dynamic _model;

        /// <summary>
        /// Constructor for initialising the <see cref="SlippageModelPythonWrapper"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model">Represents a model that simulates market order slippage</param>
        public SlippageModelPythonWrapper(PyObject model)
        {
            _model = model;
        }

        /// <summary>
        /// Slippage Model. Return a decimal cash slippage approximation on the order.
        /// </summary>
        /// <param name="asset">The security matching the order</param>
        /// <param name="order">The order to compute slippage for</param>
        /// <returns>The slippage of the order in units of the account currency</returns>
        public decimal GetSlippageApproximation(Security asset, Order order)
        {
            using (Py.GIL())
            {
                return (_model.GetSlippageApproximation(asset, order) as PyObject).GetAndDispose<decimal>();
            }
        }
    }
}