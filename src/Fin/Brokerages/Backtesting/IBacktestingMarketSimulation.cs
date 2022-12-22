using System.Collections.Generic;
using QuantConnect.Interfaces;
using QuantConnect.Packets;

namespace QuantConnect.Brokerages.Backtesting
{
    /// <summary>
    /// Backtesting Market Simulation interface, that must be implemented by all simulators of market conditions run during backtest
    /// </summary>
    public interface IBacktestingMarketSimulation
    {
        /// <summary>
        /// Method is called by backtesting brokerage to simulate market conditions. 
        /// </summary>
        /// <param name="brokerage">Backtesting brokerage instance</param>
        /// <param name="algorithm">Algorithm instance</param>
        void SimulateMarketConditions(IBrokerage brokerage, IAlgorithm algorithm);
    }
}
