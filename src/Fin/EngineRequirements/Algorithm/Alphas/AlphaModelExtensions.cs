namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Provides extension methods for alpha models
    /// </summary>
    public static class AlphaModelExtensions
    {
        /// <summary>
        /// Gets the name of the alpha model
        /// </summary>
        public static string GetModelName(this IAlphaModel model)
        {
            var namedModel = model as INamedModel;
            if (namedModel != null)
            {
                return namedModel.Name;
            }

            return model.GetType().Name;
        }
    }
}