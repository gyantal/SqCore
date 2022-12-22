namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Provides the ability to add/remove scheduled events from the real time handler
    /// </summary>
    public interface IEventSchedule
    {
        /// <summary>
        /// Adds the specified event to the schedule
        /// </summary>
        /// <param name="scheduledEvent">The event to be scheduled, including the date/times the event fires and the callback</param>
        void Add(ScheduledEvent scheduledEvent);

        /// <summary>
        /// Removes the specified event from the schedule
        /// </summary>
        /// <param name="scheduledEvent">The event to be removed</param>
        void Remove(ScheduledEvent scheduledEvent);
    }
}