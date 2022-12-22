using System;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using System.Collections.Generic;

namespace QuantConnect.Commands
{
    /// <summary>
    /// Represents a command queue for the algorithm. This is an entry point
    /// for external messages to act upon the running algorithm instance.
    /// </summary>
    public interface ICommandHandler : IDisposable
    {
        /// <summary>
        /// Initializes this command queue for the specified job
        /// </summary>
        /// <param name="job">The job that defines what queue to bind to</param>
        /// <param name="algorithm">The algorithm instance</param>
        void Initialize(AlgorithmNodePacket job, IAlgorithm algorithm);

        /// <summary>
        /// Process any commands in the queue
        /// </summary>
        /// <returns>The command result packet of each command executed if any</returns>
        IEnumerable<CommandResultPacket> ProcessCommands();
    }
}
