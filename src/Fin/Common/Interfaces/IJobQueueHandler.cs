using System.ComponentModel.Composition;
using QuantConnect.Packets;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Task requestor interface with cloud system
    /// </summary>
    [InheritedExport(typeof(IJobQueueHandler))]
    public interface IJobQueueHandler
    {
        /// <summary>
        /// Initialize the internal state
        /// </summary>
        void Initialize(IApi api);

        /// <summary>
        /// Request the next task to run through the engine:
        /// </summary>
        /// <returns>Algorithm job to process</returns>
        AlgorithmNodePacket NextJob(out string algorithmPath);

        /// <summary>
        /// Signal task complete
        /// </summary>
        /// <param name="job">Work to do.</param>
        void AcknowledgeJob(AlgorithmNodePacket job);
    }
}
