using System;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using System.ComponentModel.Composition;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Lean.Engine.Server
{
    /// <summary>
    /// Provides scope into Lean that is convenient for managing a lean instance
    /// </summary>
    [InheritedExport(typeof(ILeanManager))]
    public interface ILeanManager : IDisposable
    {
        /// <summary>
        /// Initialize the ILeanManager implementation
        /// </summary>
        /// <param name="systemHandlers">Exposes lean engine system handlers running LEAN</param>
        /// <param name="algorithmHandlers">Exposes the lean algorithm handlers running lean</param>
        /// <param name="job">The job packet representing either a live or backtest Lean instance</param>
        /// <param name="algorithmManager">The Algorithm manager</param>
        void Initialize(LeanEngineSystemHandlers systemHandlers, LeanEngineAlgorithmHandlers algorithmHandlers, AlgorithmNodePacket job, AlgorithmManager algorithmManager);

        /// <summary>
        /// Sets the IAlgorithm instance in the ILeanManager
        /// </summary>
        /// <param name="algorithm">The IAlgorithm instance being run</param>
        void SetAlgorithm(IAlgorithm algorithm);

        /// <summary>
        /// Update ILeanManager with the IAlgorithm instance
        /// </summary>
        void Update();

        /// <summary>
        /// This method is called after algorithm initialization
        /// </summary>
        void OnAlgorithmStart();

        /// <summary>
        /// This method is called before algorithm termination
        /// </summary>
        void OnAlgorithmEnd();

        /// <summary>
        /// Callback fired each time that we add/remove securities from the data feed
        /// </summary>
        void OnSecuritiesChanged(SecurityChanges changes);
    }
}
