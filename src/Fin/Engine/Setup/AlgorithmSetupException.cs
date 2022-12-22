using System;

namespace QuantConnect.Lean.Engine.Setup
{
    /// <summary>
    /// Defines an exception generated in the course of invoking <see cref="ISetupHandler.Setup"/>
    /// </summary>
    public class AlgorithmSetupException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorithmSetupException"/> class
        /// </summary>
        /// <param name="message">The error message</param>
        public AlgorithmSetupException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorithmSetupException"/> class
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="inner">The inner exception being wrapped</param>
        public AlgorithmSetupException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
