using System.ComponentModel.Composition;
using System;
using QuantConnect.Parameters;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Monitors data requests and reports on missing data
    /// </summary>
    [InheritedExport(typeof(IDataMonitor))]
    public interface IDataMonitor : IDisposable
    {
        // SqCore Change NEW:
        SqBacktestConfig SqBacktestConfig
        {
            get;
            set;
        }
        // SqCore Change END

        /// <summary>
        /// Terminates the data monitor generating a final report
        /// </summary>
        void Exit();
        
        /// <summary>
        /// Event handler for the <see cref="IDataProvider.NewDataRequest"/> event
        /// </summary>
        void OnNewDataRequest(object sender, DataProviderNewDataRequestEventArgs e);
    }
}
