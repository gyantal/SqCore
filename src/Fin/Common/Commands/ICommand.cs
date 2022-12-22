using QuantConnect.Interfaces;

namespace QuantConnect.Commands
{
    /// <summary>
    /// Represents a command that can be run against a single algorithm
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Unique command id
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Runs this command against the specified algorithm instance
        /// </summary>
        /// <param name="algorithm">The algorithm to run this command against</param>
        CommandResultPacket Run(IAlgorithm algorithm);
    }
}
