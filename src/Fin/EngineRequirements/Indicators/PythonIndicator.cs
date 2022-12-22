using Python.Runtime;
using QuantConnect.Data;
using System;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Provides a wrapper for <see cref="IndicatorBase{IBaseData}"/> implementations written in python
    /// </summary>
    public class PythonIndicator : IndicatorBase<IBaseData>, IIndicatorWarmUpPeriodProvider
    {
        private bool _isReady;
        private dynamic _indicator;

        /// <summary>
        /// Get the indicator Name. If not defined, use the class name
        /// </summary>
        /// <param name="indicator">The python implementation of <see cref="IndicatorBase{IBaseDataBar}"/></param>
        /// <returns>The indicator Name.</returns>
        private static string GetIndicatorName(PyObject indicator)
        {
            using (Py.GIL())
            {
                var name = indicator.HasAttr("Name")
                    ? indicator.GetAttr("Name")
                    : indicator.GetAttr("__class__").GetAttr("__name__");

                return name.GetAndDispose<string>();
            }
        }

        /// <summary>
        /// Get the indicator WarmUpPeriod parameter. If not defined, use 0
        /// </summary>
        /// <param name="indicator">The python implementation of <see cref="IndicatorBase{IBaseDataBar}"/></param>
        /// <returns>The WarmUpPeriod of the indicator.</returns>
        private static int GetIndicatorWarmUpPeriod(PyObject indicator)
        {
            using (Py.GIL())
            {
                var warmUpPeriod = indicator.HasAttr("WarmUpPeriod")
                    ? indicator.GetAttr("WarmUpPeriod")
                    : 0.ToPython();

                return warmUpPeriod.GetAndDispose<int>();
            }
        }

        /// <summary>
        /// Initializes a new instance of the PythonIndicator class using the specified name.
        /// </summary>
        /// <remarks>This overload allows inheritance for python classes with no arguments</remarks>
        public PythonIndicator()
            : base("")
        {
        }

        /// <summary>
        /// Initializes a new instance of the PythonIndicator class using the specified name.
        /// </summary>
        /// <remarks>This overload allows inheritance for python classes with multiple arguments</remarks>
        public PythonIndicator(params PyObject[] args)
            : base (GetIndicatorName(args[0]))
        {
        }

        /// <summary>
        /// Initializes a new instance of the PythonIndicator class using the specified name.
        /// </summary>
        /// <param name="indicator">The python implementation of <see cref="IndicatorBase{IBaseDataBar}"/></param>
        public PythonIndicator(PyObject indicator)
            : base (GetIndicatorName(indicator))
        {
            SetIndicator(indicator);
        }

        /// <summary>
        /// Sets the python implementation of the indicator
        /// </summary>
        /// <param name="indicator">The python implementation of <see cref="IndicatorBase{IBaseDataBar}"/></param>
        public void SetIndicator(PyObject indicator)
        {
            using (Py.GIL())
            {
                foreach (var attributeName in new[] {"IsReady", "Update", "Value"})
                {
                    if (!indicator.HasAttr(attributeName))
                    {
                        var name = indicator.GetAttr("__class__").GetAttr("__name__");

                        var message = $"Indicator.{attributeName} must be implemented. " +
                                      $"Please implement this missing method in {name}";

                        if (attributeName == "IsReady")
                        {
                            message += " or use PythonIndicator as base:" +
                                       $"{Environment.NewLine}class {name}(PythonIndicator):";
                        }

                        throw new NotImplementedException(message);
                    }
                }
            }

            WarmUpPeriod = GetIndicatorWarmUpPeriod(indicator);
            _indicator = indicator;
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => _isReady;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized
        /// </summary>
        public int WarmUpPeriod { get; protected set; }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IBaseData input)
        {
            using (Py.GIL())
            {
                _isReady = _indicator.Update(input) ?? _indicator.IsReady;
                return _indicator.Value;
            }
        }
    }
}
