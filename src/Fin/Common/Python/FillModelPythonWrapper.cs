using Python.Runtime;
using QuantConnect.Orders;
using QuantConnect.Orders.Fills;
using QuantConnect.Securities;

namespace QuantConnect.Python
{
    /// <summary>
    /// Wraps a <see cref="PyObject"/> object that represents a model that simulates order fill events
    /// </summary>
    public class FillModelPythonWrapper : FillModel
    {
        private readonly dynamic _model;

        /// <summary>
        /// Constructor for initialising the <see cref="FillModelPythonWrapper"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model">Represents a model that simulates order fill events</param>
        public FillModelPythonWrapper(PyObject model)
        {
            _model = model;
            using (Py.GIL())
            {
                _model.SetPythonWrapper(this);
            }
        }

        /// <summary>
        /// Return an order event with the fill details
        /// </summary>
        /// <param name="parameters">A parameters object containing the security and order</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override Fill Fill(FillModelParameters parameters)
        {
            Parameters = parameters;
            using (Py.GIL())
            {
                return (_model.Fill(parameters) as PyObject).GetAndDispose<Fill>();
            }
        }

        /// <summary>
        /// Limit Fill Model. Return an order event with the fill details.
        /// </summary>
        /// <param name="asset">Stock Object to use to help model limit fill</param>
        /// <param name="order">Order to fill. Alter the values directly if filled.</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent LimitFill(Security asset, LimitOrder order)
        {
            using (Py.GIL())
            {
                return (_model.LimitFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }

        /// <summary>
        /// Limit if Touched Fill Model. Return an order event with the fill details.
        /// </summary>
        /// <param name="asset">Asset we're trading this order</param>
        /// <param name="order"><see cref="LimitIfTouchedOrder"/> Order to Check, return filled if true</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent LimitIfTouchedFill(Security asset, LimitIfTouchedOrder order)
        {
            using (Py.GIL())
            {
                return (_model.LimitIfTouchedFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }

        /// <summary>
        /// Model the slippage on a market order: fixed percentage of order price
        /// </summary>
        /// <param name="asset">Asset we're trading this order</param>
        /// <param name="order">Order to update</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent MarketFill(Security asset, MarketOrder order)
        {
            using (Py.GIL())
            {
                return (_model.MarketFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }

        /// <summary>
        /// Market on Close Fill Model. Return an order event with the fill details
        /// </summary>
        /// <param name="asset">Asset we're trading with this order</param>
        /// <param name="order">Order to be filled</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent MarketOnCloseFill(Security asset, MarketOnCloseOrder order)
        {
            using (Py.GIL())
            {
                return (_model.MarketOnCloseFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }

        // SqCore Change NEW:
        public override OrderEvent FixPriceFill(Security asset, FixPriceOrder order)
        {
            using (Py.GIL())
            {
                return (_model.FixPriceFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }
        // SqCore Change END

        /// <summary>
        /// Market on Open Fill Model. Return an order event with the fill details
        /// </summary>
        /// <param name="asset">Asset we're trading with this order</param>
        /// <param name="order">Order to be filled</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent MarketOnOpenFill(Security asset, MarketOnOpenOrder order)
        {
            using (Py.GIL())
            {
                return (_model.MarketOnOpenFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }

        /// <summary>
        /// Stop Limit Fill Model. Return an order event with the fill details.
        /// </summary>
        /// <param name="asset">Asset we're trading this order</param>
        /// <param name="order">Stop Limit Order to Check, return filled if true</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent StopLimitFill(Security asset, StopLimitOrder order)
        {
            using (Py.GIL())
            {
                return (_model.StopLimitFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }

        /// <summary>
        /// Stop Market Fill Model. Return an order event with the fill details.
        /// </summary>
        /// <param name="asset">Asset we're trading this order</param>
        /// <param name="order">Stop Order to Check, return filled if true</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent StopMarketFill(Security asset, StopMarketOrder order)
        {
            using (Py.GIL())
            {
                return (_model.StopMarketFill(asset, order) as PyObject).GetAndDispose<OrderEvent>();
            }
        }

        /// <summary>
        /// Get the minimum and maximum price for this security in the last bar:
        /// </summary>
        /// <param name="asset">Security asset we're checking</param>
        /// <param name="direction">The order direction, decides whether to pick bid or ask</param>
        protected override Prices GetPrices(Security asset, OrderDirection direction)
        {
            using (Py.GIL())
            {
                return (_model.GetPrices(asset, direction) as PyObject).GetAndDispose<Prices>();
            }
        }

        /// <summary>
        /// Get the minimum and maximum price for this security in the last bar:
        /// </summary>
        /// <param name="asset">Security asset we're checking</param>
        /// <param name="direction">The order direction, decides whether to pick bid or ask</param>
        /// <remarks>This method was implemented temporarily to help the refactoring of fill models (GH #4567)</remarks>
        internal Prices GetPricesInternal(Security asset, OrderDirection direction)
        {
            return GetPrices(asset, direction);
        }
    }
}