﻿using System;
using System.Globalization;

namespace QuantConnect
{
    /// <summary>
    /// Provides methods for parsing strings using <see cref="CultureInfo.InvariantCulture"/>
    /// </summary>
    public static class Parse
    {
        /// <summary>
        /// Parses the provided value as a <see cref="System.TimeSpan"/> using <see cref="System.TimeSpan.Parse(string,IFormatProvider)"/>
        /// with <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static TimeSpan TimeSpan(string value)
        {
            return System.TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="System.TimeSpan"/> using <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParse(string input, out TimeSpan value)
        {
            return System.TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out value);

        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="System.TimeSpan"/>, format
        /// string, <see cref="TimeSpanStyles"/>, and using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="format"></param>
        /// <param name="timeSpanStyle"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryParseExact(string input, string format, TimeSpanStyles timeSpanStyle, out TimeSpan value)
        {
            return System.TimeSpan.TryParseExact(input, format, CultureInfo.InvariantCulture, timeSpanStyle, out value);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="System.DateTime"/> using <see cref="System.DateTime.Parse(string,IFormatProvider)"/>
        /// with <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static DateTime DateTime(string value)
        {
            return System.DateTime.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="System.DateTime"/> using <see cref="System.DateTime.ParseExact(string,string,IFormatProvider)"/>
        /// with the specified <paramref name="format"/> and <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static DateTime DateTimeExact(string value, string format)
        {
            return System.DateTime.ParseExact(value, format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="System.DateTime"/> using <see cref="System.DateTime.ParseExact(string,string,IFormatProvider)"/>
        /// with the specified <paramref name="format"/>, <paramref name="dateTimeStyles"/> and <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static DateTime DateTimeExact(string value, string format, DateTimeStyles dateTimeStyles)
        {
            return System.DateTime.ParseExact(value, format, CultureInfo.InvariantCulture, dateTimeStyles);
        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="System.DateTime"/> using the specified <paramref name="dateTimeStyle"/>
        /// and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParse(string input, DateTimeStyles dateTimeStyle, out System.DateTime value)
        {
            return System.DateTime.TryParse(input, CultureInfo.InvariantCulture, dateTimeStyle, out value);
        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="System.DateTime"/> using the
        /// specified <paramref name="dateTimeStyle"/>, the format <paramref name="format"/>, and
        /// <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParseExact(string input, string format, DateTimeStyles dateTimeStyle, out System.DateTime value)
        {
            return System.DateTime.TryParseExact(input, format, CultureInfo.InvariantCulture, dateTimeStyle, out value);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="double"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static double Double(string value)
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="double"/> using the specified <paramref name="numberStyle"/>
        /// and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParse(string input, NumberStyles numberStyle, out double value)
        {
            return double.TryParse(input, numberStyle, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="decimal"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static decimal Decimal(string value)
        {
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="decimal"/> using the specified <paramref name="numberStyles"/>
        /// and <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static decimal Decimal(string value, NumberStyles numberStyles)
        {
            return decimal.Parse(value, numberStyles, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="decimal"/> using the specified <paramref name="numberStyle"/>
        /// and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParse(string input, NumberStyles numberStyle, out decimal value)
        {
            return decimal.TryParse(input, numberStyle, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="int"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static int Int(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="int"/> using the specified <paramref name="numberStyle"/>
        /// and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParse(string input, NumberStyles numberStyle, out int value)
        {
            return int.TryParse(input, numberStyle, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="long"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static long Long(string value)
        {
            return long.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="long"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// and the specified <paramref name="numberStyles"/>
        /// </summary>
        public static long Long(string value, NumberStyles numberStyles)
        {
            return long.Parse(value, numberStyles, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tries to parse the provided value with TryParse as a <see cref="long"/> using the specified <paramref name="numberStyle"/>
        /// and <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static bool TryParse(string input, NumberStyles numberStyle, out long value)
        {
            return long.TryParse(input, numberStyle, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Parses the provided value as a an enumeration type <typeparamref name="T"/>
        /// </summary>
        public static T Enum<T>(string input, bool ignoreCase = true)
            where T : struct, IConvertible
        {
            T value;
            if (!TryParse(input, out value, ignoreCase))
            {
                throw new ArgumentException($"The provided value ({input}) was not parseable as {typeof(T).Name}");
            }

            return value;
        }

        /// <summary>
        /// Parses the provided value as a an enumeration type <typeparamref name="T"/>
        /// </summary>
        public static bool TryParse<T>(string input, out T value, bool ignoreCase = true)
            where T : struct, IConvertible
        {
            return System.Enum.TryParse<T>(input, ignoreCase, out value);
        }
    }
}