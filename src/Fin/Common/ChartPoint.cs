using System;
using Newtonsoft.Json;
using static QuantConnect.StringExtensions;

namespace QuantConnect
{
    /// <summary>
    /// Single Chart Point Value Type for QCAlgorithm.Plot();
    /// </summary>
    [JsonObject]
    public class ChartPoint
    {
        /// Time of this chart point: lower case for javascript encoding simplicty
        public long x;

        /// Value of this chart point:  lower case for javascript encoding simplicty
        public decimal y;

        /// <summary>
        /// Default constructor. Using in SeriesSampler.
        /// </summary>
        public ChartPoint() { }

        /// <summary>
        /// Constructor that takes both x, y value paris
        /// </summary>
        /// <param name="xValue">X value often representing a time in seconds</param>
        /// <param name="yValue">Y value</param>
        public ChartPoint(long xValue, decimal yValue)
        {
            x = xValue;
            y = yValue;
        }

        ///Constructor for datetime-value arguements:
        public ChartPoint(DateTime time, decimal value)
        {
            x = Convert.ToInt64(Time.DateTimeToUnixTimeStamp(time.ToUniversalTime()));
            y = value.SmartRounding();
        }

        ///Cloner Constructor:
        public ChartPoint(ChartPoint point)
        {
            x = point.x;
            y = point.y.SmartRounding();
        }

        /// <summary>
        /// Provides a readable string representation of this instance.
        /// </summary>
        public override string ToString()
        {
            return Invariant($"{Time.UnixTimeStampToDateTime(x):o} - {y}");
        }
    }
}
