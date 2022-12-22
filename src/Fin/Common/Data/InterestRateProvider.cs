using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Util;
using SqCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QuantConnect.Data
{
    /// <summary>
    /// Fed US Primary Credit Rate at given date
    /// </summary>
    public class InterestRateProvider
    {
        private static readonly DateTime FirstInterestRateDate = new DateTime(1998, 1, 1);

        /// <summary>
        /// Default Risk Free Rate of 1%
        /// </summary>
        public static decimal DefaultRiskFreeRate { get; } = 0.01m;

        private DateTime _lastInterestRateDate;
        private Dictionary<DateTime, decimal> _riskFreeRateProvider;

        /// <summary>
        /// Create class instance of interest rate provider
        /// </summary>
        public InterestRateProvider()
        {
            LoadInterestRateProvider();
        }

        /// <summary>
        /// Get interest rate by a given datetime
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns>interest rate of the given date</returns>
        public decimal GetInterestRate(DateTime dateTime)
        {
            if (!_riskFreeRateProvider.TryGetValue(dateTime, out var interestRate))
            {
                return dateTime < FirstInterestRateDate
                    ? _riskFreeRateProvider[FirstInterestRateDate]
                    : _riskFreeRateProvider[_lastInterestRateDate];
            }

            return interestRate;
        }

        /// <summary>
        /// Generate the daily historical US primary credit rate
        /// </summary>
        protected void LoadInterestRateProvider()
        {
            var directory = Path.Combine(Globals.DataFolder, "alternative", "interest-rate", "usa",
                "interest-rate.csv");
            _riskFreeRateProvider = FromCsvFile(directory, out var previousInterestRate);

            _lastInterestRateDate = DateTime.UtcNow.Date;

            // Sparse the discrete data points into continuous credit rate data for every day
            for (var date = FirstInterestRateDate; date <= _lastInterestRateDate; date = date.AddDays(1))
            {
                if (!_riskFreeRateProvider.TryGetValue(date, out var currentRate))
                {
                    _riskFreeRateProvider[date] = previousInterestRate;
                    continue;
                }

                previousInterestRate = currentRate;
            }
        }

        /// <summary>
        /// Reads Fed primary credit rate file and returns a dictionary of historical rate changes
        /// </summary>
        /// <param name="file">The csv file to be read</param>
        /// <param name="firstInterestRate">The first interest rate on file</param>
        /// <returns>Dictionary of historical credit rate change events</returns>
        public static Dictionary<DateTime, decimal> FromCsvFile(string file, out decimal firstInterestRate)
        {
            var dataProvider = Composer.Instance.GetExportedValueByTypeName<IDataProvider>(
                Config.Get("data-provider", "DefaultDataProvider"));

            var firstInterestRateSet = false;
            firstInterestRate = DefaultRiskFreeRate;

            // skip the first header line, also skip #'s as these are comment lines
            var interestRateProvider = new Dictionary<DateTime, decimal>();
            foreach (var line in dataProvider.ReadLines(file).Skip(1)
                         .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (TryParse(line, out var date, out var interestRate))
                {
                    if (!firstInterestRateSet)
                    {
                        firstInterestRate = interestRate;
                        firstInterestRateSet = true;
                    }

                    interestRateProvider[date] = interestRate;
                }
            }

            if (interestRateProvider.Count == 0)
            {
                Utils.Logger.Error($"InterestRateProvider.FromCsvFile(): no interest rates were loaded, please make sure the file is present '{file}'");
            }

            return interestRateProvider;
        }

        /// <summary>
        /// Parse the string into the interest rate date and value
        /// </summary>
        /// <param name="csvLine">The csv line to be parsed</param>
        /// <param name="date">Parsed interest rate date</param>
        /// <param name="interestRate">Parsed interest rate value</param>
        public static bool TryParse(string csvLine, out DateTime date, out decimal interestRate)
        {
            var line = csvLine.Split(',');

            if (!DateTime.TryParseExact(line[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                Utils.Logger.Error($"Couldn't parse date/time while reading FED primary credit rate file. Line: {csvLine}");
                interestRate = DefaultRiskFreeRate;
                return false;
            }

            if (!decimal.TryParse(line[1], out interestRate))
            {
                Utils.Logger.Error($"Couldn't parse primary credit rate while reading FED primary credit rate file. Line: {csvLine}");
                return false;
            }

            // Unit conversion from % 
            interestRate /= 100;
            return true;
        }
    }
}
