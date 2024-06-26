using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Commands;
using QuantConnect.Interfaces;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Lean.Engine.DataFeeds.Transport;

namespace QuantConnect.Lean.Engine.Server
{
    /// <summary>
    /// NOP implementation of the ILeanManager interface
    /// </summary>
    public class LocalLeanManager : ILeanManager
    {
        private IAlgorithm _algorithm;
        private AlgorithmNodePacket _job;
        private ICommandHandler _commandHandler;
        private LeanEngineSystemHandlers _systemHandlers;
        private LeanEngineAlgorithmHandlers _algorithmHandlers;

        /// <summary>
        /// Empty implementation of the ILeanManager interface
        /// </summary>
        /// <param name="systemHandlers">Exposes lean engine system handlers running LEAN</param>
        /// <param name="algorithmHandlers">Exposes the lean algorithm handlers running lean</param>
        /// <param name="job">The job packet representing either a live or backtest Lean instance</param>
        /// <param name="algorithmManager">The Algorithm manager</param>
        public void Initialize(LeanEngineSystemHandlers systemHandlers, LeanEngineAlgorithmHandlers algorithmHandlers, AlgorithmNodePacket job, AlgorithmManager algorithmManager)
        {
            _algorithmHandlers = algorithmHandlers;
            _systemHandlers = systemHandlers;
            _job = job;
        }

        /// <summary>
        /// Sets the IAlgorithm instance in the ILeanManager
        /// </summary>
        /// <param name="algorithm">The IAlgorithm instance being run</param>
        public void SetAlgorithm(IAlgorithm algorithm)
        {
            _algorithm = algorithm;
            algorithm.SetApi(_systemHandlers.Api);
            RemoteFileSubscriptionStreamReader.SetDownloadProvider((Api.Api)_systemHandlers.Api);
        }

        /// <summary>
        /// Execute the commands using the IAlgorithm instance
        /// </summary>
        public void Update()
        {
            if(_commandHandler != null)
            {
                foreach (var commandResultPacket in _commandHandler.ProcessCommands())
                {
                    _algorithmHandlers.Results.Messages.Enqueue(commandResultPacket);
                }
            }
        }

        /// <summary>
        /// This method is called after algorithm initialization
        /// </summary>
        public void OnAlgorithmStart()
        {
            if (_algorithm.LiveMode)
            {
                _commandHandler = new FileCommandHandler();
                _commandHandler.Initialize(_job, _algorithm);
            }
        }

        /// <summary>
        /// This method is called before algorithm termination
        /// </summary>
        public void OnAlgorithmEnd()
        {
            // NOP
        }

        /// <summary>
        /// Callback fired each time that we add/remove securities from the data feed
        /// </summary>
        public void OnSecuritiesChanged(SecurityChanges changes)
        {
            // NOP
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _commandHandler.DisposeSafely();
        }
    }
}
