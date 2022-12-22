namespace QuantConnect.Optimizer.Objectives
{
    /// <summary>
    /// Defines standard maximization strategy, i.e. right operand is greater than left
    /// </summary>
    public class Maximization : Extremum
    {
        /// <summary>
        /// Creates an instance of <see cref="Maximization"/>
        /// </summary>
        public Maximization() : base((v1, v2) => v1 < v2)
        {
        }
    }
}
