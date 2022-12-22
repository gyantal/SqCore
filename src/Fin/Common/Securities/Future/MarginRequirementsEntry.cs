using System;
using System.Globalization;
using SqCommon;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// POCO class for modeling margin requirements at given date
    /// </summary>
    public class MarginRequirementsEntry
    {
        /// <summary>
        /// Date of margin requirements change
        /// </summary>
        public DateTime Date { get; init; }

        /// <summary>
        /// Initial overnight margin for the contract effective from the date of change
        /// </summary>
        public decimal InitialOvernight { get; init; }

        /// <summary>
        /// Maintenance overnight margin for the contract effective from the date of change
        /// </summary>
        public decimal MaintenanceOvernight { get; init; }

        /// <summary>
        /// Initial intraday margin for the contract effective from the date of change
        /// </summary>
        public decimal InitialIntraday { get; init; }

        /// <summary>
        /// Maintenance intraday margin for the contract effective from the date of change
        /// </summary>
        public decimal MaintenanceIntraday { get; init; }

        /// <summary>
        /// Creates a new instance of <see cref="MarginRequirementsEntry"/> from the specified csv line
        /// </summary>
        /// <param name="csvLine">The csv line to be parsed</param>
        /// <returns>A new <see cref="MarginRequirementsEntry"/> for the specified csv line</returns>
        public static MarginRequirementsEntry Create(string csvLine)
        {
            var line = csvLine.Split(',');

            DateTime date;
            if (!DateTime.TryParseExact(line[0], DateFormat.EightCharacter, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                Utils.Logger.Trace($"Couldn't parse date/time while reading future margin requirement file. Line: {csvLine}");
            }

            decimal initialOvernight;
            if (!decimal.TryParse(line[1], out initialOvernight))
            {
                Utils.Logger.Trace($"Couldn't parse Initial Overnight margin requirements while reading future margin requirement file. Line: {csvLine}");
            }

            decimal maintenanceOvernight;
            if (!decimal.TryParse(line[2], out maintenanceOvernight))
            {
                Utils.Logger.Trace($"Couldn't parse Maintenance Overnight margin requirements while reading future margin requirement file. Line: {csvLine}");
            }

            // default value, if present in file we try to parse
            decimal initialIntraday = initialOvernight * 0.4m;
            if (line.Length >= 4
                && !decimal.TryParse(line[3], out initialIntraday))
            {
                Utils.Logger.Trace($"Couldn't parse Initial Intraday margin requirements while reading future margin requirement file. Line: {csvLine}");
            }

            // default value, if present in file we try to parse
            decimal maintenanceIntraday = maintenanceOvernight * 0.4m;
            if (line.Length >= 5
                && !decimal.TryParse(line[4], out maintenanceIntraday))
            {
                Utils.Logger.Trace($"Couldn't parse Maintenance Intraday margin requirements while reading future margin requirement file. Line: {csvLine}");
            }

            return new MarginRequirementsEntry
            {
                Date = date,
                InitialOvernight = initialOvernight,
                MaintenanceOvernight = maintenanceOvernight,
                InitialIntraday = initialIntraday,
                MaintenanceIntraday = maintenanceIntraday
            };
        }
    }
}
