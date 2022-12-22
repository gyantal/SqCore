using System.ComponentModel.Composition;
using QuantConnect.Data.Auxiliary;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Provides instances of <see cref="FactorFile"/> at run time
    /// </summary>
    [InheritedExport(typeof(IFactorFileProvider))]
    public interface IFactorFileProvider
    {
        /// <summary>
        /// Initializes our FactorFileProvider by supplying our mapFileProvider
        /// and dataProvider
        /// </summary>
        /// <param name="mapFileProvider">MapFileProvider to use</param>
        /// <param name="dataProvider">DataProvider to use</param>
        void Initialize(IMapFileProvider mapFileProvider, IDataProvider dataProvider);

        /// <summary>
        /// Gets a <see cref="FactorFile"/> instance for the specified symbol, or null if not found
        /// </summary>
        /// <param name="symbol">The security's symbol whose factor file we seek</param>
        /// <returns>The resolved factor file, or null if not found</returns>
        IFactorProvider Get(Symbol symbol);
    }
}
