using System;
using QuantConnect.Data.Market;
using Python.Runtime;

namespace QuantConnect.Data.Consolidators
{
    /// <summary>
    /// Type capable of consolidating open interest
    /// </summary>
    public class OpenInterestConsolidator : PeriodCountConsolidatorBase<Tick, OpenInterest>
    {
        /// <summary>
        /// Create a new OpenInterestConsolidator for the desired resolution
        /// </summary>
        /// <param name="resolution">The resolution desired</param>
        /// <returns>A consolidator that produces data on the resolution interval</returns>
        public static OpenInterestConsolidator FromResolution(Resolution resolution)
        {
            return new OpenInterestConsolidator(resolution.ToTimeSpan());
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'OpenInterest' representing the period
        /// </summary>
        /// <param name="period">The minimum span of time before emitting a consolidated bar</param>
        public OpenInterestConsolidator(TimeSpan period)
            : base(period)
        {
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'OpenInterest' representing the last count pieces of data
        /// </summary>
        /// <param name="maxCount">The number of pieces to accept before emitting a consolidated bar</param>
        public OpenInterestConsolidator(int maxCount)
            : base(maxCount)
        {
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'OpenInterest' representing the last count pieces of data or the period, whichever comes first
        /// </summary>
        /// <param name="maxCount">The number of pieces to accept before emitting a consolidated bar</param>
        /// <param name="period">The minimum span of time before emitting a consolidated bar</param>
        public OpenInterestConsolidator(int maxCount, TimeSpan period)
            : base(maxCount, period)
        {
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'OpenInterest'
        /// </summary>
        /// <param name="func">Func that defines the start time of a consolidated data</param>
        public OpenInterestConsolidator(Func<DateTime, CalendarInfo> func)
            : base(func)
        {
        }

        /// <summary>
        /// Creates a consolidator to produce a new 'OpenInterest'
        /// </summary>
        /// <param name="pyfuncobj">Python function object that defines the start time of a consolidated data</param>
        public OpenInterestConsolidator(PyObject pyfuncobj)
            : base(pyfuncobj)
        {
        }


        /// <summary>
        /// Determines whether or not the specified data should be processed
        /// </summary>
        /// <param name="tick">The data to check</param>
        /// <returns>True if the consolidator should process this data, false otherwise</returns>
        protected override bool ShouldProcess(Tick tick)
        {
            return tick.TickType == TickType.OpenInterest;
        }

        /// <summary>
        /// Aggregates the new 'data' into the 'workingBar'. The 'workingBar' will be
        /// null following the event firing
        /// </summary>
        /// <param name="workingBar">The bar we're building, null if the event was just fired and we're starting a new OI bar</param>
        /// <param name="tick">The new data</param>
        protected override void AggregateBar(ref OpenInterest workingBar, Tick tick)
        {
            if (workingBar == null)
            {
                workingBar = new OpenInterest
                {
                    Symbol = tick.Symbol,
                    Time = GetRoundedBarTime(tick),
                    Value = tick.Value
                };

            }
            else
            {
                //Update the working bar
                workingBar.Value = tick.Value;
            }
        }
    }
}
