﻿namespace QuantConnect.Securities
{
    /// <summary>
    /// Static class contains definitions of popular futures expiration cycles
    /// </summary>
    public static class FutureExpirationCycles
    {
        /// <summary>
        /// January Cycle: Expirations in January, April, July, October (the first month of each quarter)
        /// </summary>
        public static readonly int[] January = { 1, 4, 7, 10 };

        /// <summary>
        /// February Cycle: Expirations in February, May, August, November (second month)
        /// </summary>
        public static readonly int[] February = { 2, 5, 8, 11 };

        /// <summary>
        /// March Cycle: Expirations in March, June, September, December (third month)
        /// </summary>
        public static readonly int[] March = { 3, 6, 9, 12 };

        /// <summary>
        /// All Year Cycle: Expirations in every month of the year
        /// </summary>
        public static readonly int[] AllYear = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        /// <summary>
        /// GJMQVZ Cycle
        /// </summary>
        public static readonly int[] GJMQVZ = { 2, 4, 6, 8, 10, 12 };

        /// <summary>
        /// GJKMNQVZ Cycle
        /// </summary>
        public static readonly int[] GJKMNQVZ = { 2, 4, 5, 6, 7, 8, 10, 12 };

        /// <summary>
        /// HMUZ Cycle
        /// </summary>
        public static readonly int[] HMUZ = March;

        /// <summary>
        /// HKNUZ Cycle
        /// </summary>
        public static readonly int[] HKNUZ = { 3, 5, 7, 9, 12 };

        /// <summary>
        /// HKNV Cycle
        /// </summary>
        public static readonly int[] HKNV = { 3, 5, 7, 10 };

        /// <summary>
        /// HKNVZ Cycle
        /// </summary>
        public static readonly int[] HKNVZ = { 3, 5, 7, 10, 12 };

        /// <summary>
        /// FHKNUX Cycle
        /// </summary>
        public static readonly int[] FHKNUX = { 1, 3, 5, 7, 9, 11 };

        /// <summary>
        /// FHJKQUVX Cycle
        /// </summary>
        public static readonly int[] FHJKQUVX = { 1, 3, 4, 5, 8, 9, 10, 11 };

        /// <summary>
        /// HKNUVZ Cycle
        /// </summary>
        public static readonly int[] HKNUVZ = { 3, 5, 7, 9, 10, 12 };

        /// <summary>
        /// FHKNQUVZ Cycle
        /// </summary>
        public static readonly int[] FHKNUVZ = { 1, 3, 5, 7, 9, 10, 12 };

        /// <summary>
        /// FHKMQUVZ Cycle
        /// </summary>
        public static readonly int[] FHKNQUVZ = { 1, 3, 5, 7, 8, 9, 10, 12 };

        /// <summary>
        /// FHKNQUX Cycle
        /// </summary>
        public static readonly int[] FHKNQUX = { 1, 3, 5, 7, 8, 9, 11 };

        /// <summary>
        /// FGHJKMNQUVXZ Cycle
        /// </summary>
        public static readonly int[] FGHJKMNQUVXZ = AllYear;
    }
}
