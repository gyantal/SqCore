using Newtonsoft.Json;

namespace QuantConnect.Optimizer.Parameters
{
    /// <summary>
    /// Defines the optimization parameter meta information
    /// </summary>
    [JsonConverter(typeof(OptimizationParameterJsonConverter))]
    public abstract class OptimizationParameter
    {
        /// <summary>
        /// Name of optimization parameter
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; }

        /// <summary>
        /// Create an instance of <see cref="OptimizationParameter"/> based on configuration
        /// </summary>
        /// <param name="name">parameter name</param>
        protected OptimizationParameter(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        public bool Equals(OptimizationParameter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other?.Name);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object. </param>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as OptimizationParameter);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>
        /// A hash code for the current object.
        /// </returns>
        public override int GetHashCode() => this.Name.GetHashCode();
    }
}
