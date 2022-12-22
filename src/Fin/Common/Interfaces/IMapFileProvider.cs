using System.ComponentModel.Composition;
using QuantConnect.Data.Auxiliary;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Provides instances of <see cref="MapFileResolver"/> at run time
    /// </summary>
    [InheritedExport(typeof(IMapFileProvider))]
    public interface IMapFileProvider
    {
        /// <summary>
        /// Initializes our MapFileProvider by supplying our dataProvider
        /// </summary>
        /// <param name="dataProvider">DataProvider to use</param>
        void Initialize(IDataProvider dataProvider);

        /// <summary>
        /// Gets a <see cref="MapFileResolver"/> representing all the map
        /// files for the specified market
        /// </summary>
        /// <param name="auxiliaryDataKey">Key used to fetch a map file resolver. Specifying market and security type</param>
        /// <returns>A <see cref="MapFileResolver"/> containing all map files for the specified market</returns>
        MapFileResolver Get(AuxiliaryDataKey auxiliaryDataKey);
    }
}
