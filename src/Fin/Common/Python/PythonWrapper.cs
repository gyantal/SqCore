using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Python.Runtime;

namespace QuantConnect.Python
{
    /// <summary>
    /// Provides extension methods for managing python wrapper classes
    /// </summary>
    public static class PythonWrapper
    {
        /// <summary>
        /// Validates that the specified <see cref="PyObject"/> completely implements the provided interface type
        /// </summary>
        /// <typeparam name="TInterface">The interface type</typeparam>
        /// <param name="model">The model implementing the interface type</param>
        public static PyObject ValidateImplementationOf<TInterface>(this PyObject model)
        {
            if (!typeof(TInterface).IsInterface)
            {
                throw new ArgumentException($"{nameof(PythonWrapper)}.{nameof(ValidateImplementationOf)} expected an interface type parameter.");
            }

            var missingMembers = new List<string>();
            var members = typeof(TInterface).GetMembers(BindingFlags.Public | BindingFlags.Instance);
            using (Py.GIL())
            {
                foreach (var member in members)
                {
                    if (!model.HasAttr(member.Name))
                    {
                        missingMembers.Add(member.Name);
                    }
                }

                if (missingMembers.Any())
                {
                    throw new NotImplementedException($"{nameof(TInterface)} must be fully implemented. Please implement " +
                        $"these missing methods on {model.GetPythonType()}: {string.Join(", ", missingMembers)}"
                    );
                }
            }

            return model;
        }
    }
}