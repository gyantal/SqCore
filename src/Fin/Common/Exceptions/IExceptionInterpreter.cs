using System;
using QuantConnect.Interfaces;

namespace QuantConnect.Exceptions
{
    /// <summary>
    /// Defines an exception interpreter. Interpretations are invoked on <see cref="IAlgorithm.RunTimeError"/>
    /// </summary>
    public interface IExceptionInterpreter
    {
        /// <summary>
        /// Determines the order that a class that implements this interface should be called
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Determines if this interpreter should be applied to the specified exception.
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if the exception can be interpreted, false otherwise</returns>
        bool CanInterpret(Exception exception);

        /// <summary>
        /// Interprets the specified exception into a new exception
        /// </summary>
        /// <param name="exception">The exception to be interpreted</param>
        /// <param name="innerInterpreter">An interpreter that should be applied to the inner exception.
        /// This provides a link back allowing the inner exceptions to be interpreted using the interpreters
        /// configured in the <see cref="IExceptionInterpreter"/>. Individual implementations *may* ignore
        /// this value if required.</param>
        /// <returns>The interpreted exception</returns>
        Exception Interpret(Exception exception, IExceptionInterpreter innerInterpreter);
    }
}