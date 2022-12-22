namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Provides a marker interface allowing models to define their own names.
    /// If not specified, the framework will use the model's type name.
    /// Implementation of this is not required unless you plan on running multiple models
    /// of the same type w/ different parameters.
    /// </summary>
    public interface INamedModel
    {
        /// <summary>
        /// Defines a name for a framework model
        /// </summary>
        string Name { get; }
    }
}