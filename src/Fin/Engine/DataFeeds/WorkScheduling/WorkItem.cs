﻿using System;
using System.Runtime.CompilerServices;

namespace QuantConnect.Lean.Engine.DataFeeds.WorkScheduling
{
    /// <summary>
    /// Class to represent a work item
    /// </summary>
    public class WorkItem
    {
        /// <summary>
        /// Function to determine weight of item
        /// </summary>
        private Func<int> _weightFunc;

        /// <summary>
        /// The current weight
        /// </summary>
        public int Weight { get; private set; }

        /// <summary>
        /// The work function to execute
        /// </summary>
        public Func<int, bool> Work { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="work">The work function, takes an int, the amount of work to do
        /// and returns a bool, false if this work item is finished</param>
        /// <param name="weightFunc">The function used to determine the current weight</param>
        public WorkItem(Func<int, bool> work, Func<int> weightFunc)
        {
            Work = work;
            _weightFunc = weightFunc;
        }

        /// <summary>
        /// Updates the weight of this work item
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int UpdateWeight()
        {
            Weight = _weightFunc();
            return Weight;
        }

        /// <summary>
        /// Compares two work items based on their weights
        /// </summary>
        public static int Compare(WorkItem obj, WorkItem other)
        {
            if (ReferenceEquals(obj, other))
            {
                return 0;
            }
            // By definition, any object compares greater than null
            if (ReferenceEquals(obj, null))
            {
                return -1;
            }
            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            return other.Weight.CompareTo(obj.Weight);
        }
    }
}
