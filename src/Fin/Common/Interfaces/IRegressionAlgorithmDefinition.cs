using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Defines a C# algorithm as a regression algorithm to be run as part of the test suite.
    /// This interface also allows the algorithm to declare that it has versions in other languages
    /// that should yield identical results.
    /// </summary>
    public interface IRegressionAlgorithmDefinition
    {
        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        bool CanRunLocally { get; }

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        Language[] Languages { get; }

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        long DataPoints { get; }

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        int AlgorithmHistoryDataPoints { get; }

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        Dictionary<string, string> ExpectedStatistics { get; }
    }
}
