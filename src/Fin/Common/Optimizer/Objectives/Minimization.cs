namespace QuantConnect.Optimizer.Objectives
{
    /// <summary>
    /// Defines standard minimization strategy, i.e. right operand is less than left
    /// </summary>
    public class Minimization : Extremum
    {
        /// <summary>
        /// Creates an instance of <see cref="Minimization"/>
        /// </summary>
        public Minimization() : base((v1, v2) => v1 > v2)
        {
        }
    }
}
