﻿using System;
using QuantConnect.Scheduling;

namespace QuantConnect
{
    /// <summary>
    /// Provides access to the <see cref="NullIsolatorLimitResultProvider"/> and extension methods supporting <see cref="ScheduledEvent"/>
    /// </summary>
    public static class IsolatorLimitResultProvider
    {
        /// <summary>
        /// Provides access to a null implementation of <see cref="IIsolatorLimitResultProvider"/>
        /// </summary>
        public static readonly IIsolatorLimitResultProvider Null = new NullIsolatorLimitResultProvider();

        /// <summary>
        /// Convenience method for invoking a scheduled event's Scan method inside the <see cref="IsolatorLimitResultProvider"/>
        /// </summary>
        public static void Consume(
            this IIsolatorLimitResultProvider isolatorLimitProvider,
            ScheduledEvent scheduledEvent,
            DateTime scanTimeUtc,
            TimeMonitor timeMonitor
            )
        {
            // perform initial filtering to prevent starting a task when not necessary
            if (scheduledEvent.NextEventUtcTime > scanTimeUtc)
            {
                return;
            }

            var timeProvider = RealTimeProvider.Instance;
            isolatorLimitProvider.Consume(timeProvider, () => scheduledEvent.Scan(scanTimeUtc), timeMonitor);
        }

        /// <summary>
        /// Executes the provided code block and while the code block is running, continually consume from
        /// the limit result provided one token each minute. This function allows the code to run for the
        /// first full minute without requesting additional time from the provider. Following that, every
        /// minute an additional one minute will be requested from the provider.
        /// </summary>
        /// <remarks>
        /// This method exists to support scheduled events, and as such, intercepts any errors raised via the
        /// provided code and wraps them in a <see cref="ScheduledEventException"/>. If in the future this is
        /// usable elsewhere, consider refactoring to handle the errors in a different fashion.
        /// </remarks>
        public static void Consume(
            this IIsolatorLimitResultProvider isolatorLimitProvider,
            ITimeProvider timeProvider,
            Action code,
            TimeMonitor timeMonitor
            )
        {
            var consumer = new TimeConsumer
            {
                IsolatorLimitProvider = isolatorLimitProvider,
                TimeProvider = timeProvider
            };
            timeMonitor.Add(consumer);
            code();
            consumer.Finished = true;
        }


        private sealed class NullIsolatorLimitResultProvider : IIsolatorLimitResultProvider
        {
            private static readonly IsolatorLimitResult OK = new IsolatorLimitResult(TimeSpan.Zero, string.Empty);

            public void RequestAdditionalTime(int minutes) { }
            public IsolatorLimitResult IsWithinLimit() { return OK; }
            public bool TryRequestAdditionalTime(int minutes) { return true; }
        }
    }
}