using System;

namespace QuantConnect.Data.Consolidators
{
    /// <summary>
    /// Helper class that provides <see cref="Func{DateTime,CalendarInfo}"/> used to define consolidation calendar
    /// </summary>
    public static class Calendar
    {
        /// <summary>
        /// Computes the start of week (previous Monday) of given date/time
        /// </summary>
        public static Func<DateTime, CalendarInfo> Weekly
        {
            get
            {
                return dt =>
                {
                    var start = Expiry.EndOfWeek(dt).AddDays(-7);
                    return new CalendarInfo(start, TimeSpan.FromDays(7));
                };
            }
        }

        /// <summary>
        /// Computes the start of month (1st of the current month) of given date/time
        /// </summary>
        public static Func<DateTime, CalendarInfo> Monthly
        {
            get
            {
                return dt =>
                {
                    var start = dt.AddDays(1 - dt.Day).Date;
                    var end = Expiry.EndOfMonth(dt);
                    return new CalendarInfo(start, end - start);
                };
            }
        }

        /// <summary>
        /// Computes the start of quarter (1st of the starting month of current quarter) of given date/time
        /// </summary>
        public static Func<DateTime, CalendarInfo> Quarterly
        {
            get
            {
                return dt =>
                {
                    var nthQuarter = (dt.Month - 1) / 3;
                    var firstMonthOfQuarter = nthQuarter * 3 + 1;
                    var start = new DateTime(dt.Year, firstMonthOfQuarter, 1);
                    var end = Expiry.EndOfQuarter(dt);
                    return new CalendarInfo(start, end - start);
                };
            }
        }

        /// <summary>
        /// Computes the start of year (1st of the current year) of given date/time
        /// </summary>
        public static Func<DateTime, CalendarInfo> Yearly
        {
            get
            {
                return dt =>
                {
                    var start = dt.AddDays(1 - dt.DayOfYear).Date;
                    var end = Expiry.EndOfYear(dt);
                    return new CalendarInfo(start, end - start);
                };
            }
        }
    }

    /// <summary>
    /// Calendar Info for storing information related to the start and period of a consolidator
    /// </summary>
    public struct CalendarInfo
    {
        /// <summary>
        /// Calendar Start
        /// </summary>
        public readonly DateTime Start;

        /// <summary>
        /// Consolidation Period
        /// </summary>
        public readonly TimeSpan Period;

        /// <summary>
        /// Constructor for CalendarInfo; used for consolidation calendar
        /// </summary>
        /// <param name="start">Calendar Start</param>
        /// <param name="period">Consolidation Period</param>
        public CalendarInfo(DateTime start, TimeSpan period)
        {
            Start = start;
            Period = period;
        }
    }
}
