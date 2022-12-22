using Python.Runtime;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;

namespace QuantConnect.Python
{
    /// <summary>
    /// Provides an order fee model that wraps a <see cref="PyObject"/> object that represents a model that simulates order fees
    /// </summary>
    public class FeeModelPythonWrapper : FeeModel
    {
        private readonly dynamic _model;
        private bool _extendedVersion = true;

        /// <summary>
        /// Constructor for initialising the <see cref="FeeModelPythonWrapper"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model">Represents a model that simulates order fees</param>
        public FeeModelPythonWrapper(PyObject model)
        {
            _model = model;
        }

        /// <summary>
        /// Get the fee for this order
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in units of the account currency</returns>
        public override OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            using (Py.GIL())
            {
                if (_extendedVersion)
                {
                    try
                    {
                        return (_model.GetOrderFee(parameters) as PyObject).GetAndDispose<OrderFee>();
                    }
                    catch (PythonException)
                    {
                        _extendedVersion = false;
                    }
                }
                var fee = (_model.GetOrderFee(parameters.Security, parameters.Order)
                    as PyObject).GetAndDispose<decimal>();
                return new OrderFee(new CashAmount(fee, "USD"));
            }
        }
    }
}