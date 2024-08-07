﻿using System;
using System.Collections.Generic;

namespace QuantConnect
{
    /// <summary>
    /// Enum lists available trading events
    /// </summary>
    public enum TradingDayType
    {
        /// <summary>
        /// Business day (0)
        /// </summary>
        BusinessDay,

        /// <summary>
        /// Public Holiday (1)
        /// </summary>
        PublicHoliday,

        /// <summary>
        /// Weekend (2)
        /// </summary>
        Weekend,

        /// <summary>
        /// Option Expiration Date (3)
        /// </summary>
        OptionExpiration,

        /// <summary>
        /// Futures Expiration Date (4)
        /// </summary>
        FutureExpiration,

        /// <summary>
        /// Futures Roll Date (5)
        /// </summary>
        /// <remarks>Not used yet. For future use.</remarks>
        FutureRoll,

        /// <summary>
        /// Symbol Delisting Date (6)
        /// </summary>
        /// <remarks>Not used yet. For future use.</remarks>
        SymbolDelisting,

        /// <summary>
        /// Equity Ex-dividend Date (7)
        /// </summary>
        /// <remarks>Not used yet. For future use.</remarks>
        EquityDividends,

        /// <summary>
        /// FX Economic Event (8)
        /// </summary>
        /// <remarks>FX Economic Event e.g. from DailyFx (DailyFx.cs). Not used yet. For future use.</remarks>
        EconomicEvent
    }

    /// <summary>
    /// Class contains trading events associated with particular day in <see cref="TradingCalendar"/>
    /// </summary>
    public class TradingDay
    {
        /// <summary>
        /// The date that this instance is associated with
        /// </summary>
        public DateTime Date { get; internal set; }

        /// <summary>
        /// Property returns true, if the day is a business day
        /// </summary>
        public bool BusinessDay { get; internal set; }

        /// <summary>
        /// Property returns true, if the day is a public holiday
        /// </summary>
        public bool PublicHoliday { get; internal set; }

        /// <summary>
        /// Property returns true, if the day is a weekend
        /// </summary>
        public bool Weekend { get; internal set; }

        /// <summary>
        /// Property returns the list of options (among currently traded) that expire on this day
        /// </summary>
        public IEnumerable<Symbol> OptionExpirations { get; internal set; }

        /// <summary>
        /// Property returns the list of futures (among currently traded) that expire on this day
        /// </summary>
        public IEnumerable<Symbol> FutureExpirations { get; internal set; }

        /// <summary>
        /// Property returns the list of futures (among currently traded) that roll forward on this day
        /// </summary>
        /// <remarks>Not used yet. For future use.</remarks>
        public IEnumerable<Symbol> FutureRolls { get; internal set; }

        /// <summary>
        /// Property returns the list of symbols (among currently traded) that are delisted on this day
        /// </summary>
        /// <remarks>Not used yet. For future use.</remarks>
        public IEnumerable<Symbol> SymbolDelistings { get; internal set; }

        /// <summary>
        /// Property returns the list of symbols (among currently traded) that have ex-dividend date on this day
        /// </summary>
        /// <remarks>Not used yet. For future use.</remarks>
        public IEnumerable<Symbol> EquityDividends { get; internal set; }
    }
}
