using System.ComponentModel.Composition;
using System;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Monitors data requests and reports on missing data
    /// </summary>
    [InheritedExport(typeof(IDataMonitor))]
    public interface IDataMonitor : IDisposable
    {
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
