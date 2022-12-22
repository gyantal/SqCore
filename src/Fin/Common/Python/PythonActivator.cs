using Python.Runtime;
using System;

namespace QuantConnect.Python
{
    /// <summary>
    /// Provides methods for creating new instances of python custom data objects
    /// </summary>
    public class PythonActivator
    {
        /// <summary>
        /// <see cref="System.Type"/> of the object we wish to create
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Method to return an instance of object
        /// </summary>
        public Func<object[], object> Factory { get;  }

        /// <summary>
        /// Creates a new instance of <see cref="PythonActivator"/>
        /// </summary>
        /// <param name="type"><see cref="System.Type"/> of the object we wish to create</param>
        /// <param name="value"><see cref="PyObject"/> that contains the python type</param>
        public PythonActivator(Type type, PyObject value)
        {
            Type = type;

            Factory = x =>
            {
                using (Py.GIL())
                {
                    var instance = value.Invoke();
                    return new PythonData(instance);
                }
            };
        }
    }
}