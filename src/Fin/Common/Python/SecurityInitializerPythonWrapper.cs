using Python.Runtime;
using QuantConnect.Securities;

namespace QuantConnect.Python
{
    /// <summary>
    /// Wraps a <see cref="PyObject"/> object that represents a type capable of initializing a new security
    /// </summary>
    public class SecurityInitializerPythonWrapper : ISecurityInitializer
    {
        private readonly dynamic _model;

        /// <summary>
        /// Constructor for initialising the <see cref="SecurityInitializerPythonWrapper"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model">Represents a type capable of initializing a new security</param>
        public SecurityInitializerPythonWrapper(PyObject model)
        {
            _model = model;
        }

        /// <summary>
        /// Initializes the specified security
        /// </summary>
        /// <param name="security">The security to be initialized</param>
        public void Initialize(Security security)
        {
            using (Py.GIL())
            {
                _model.Initialize(security);
            }
        }
    }
}