namespace QuantConnect.Data.Custom.Tiingo
{
    /// <summary>
    /// Helper class for Tiingo configuration
    /// </summary>
    public static class Tiingo
    {
        /// <summary>
        /// Gets the Tiingo API token.
        /// </summary>
        public static string AuthCode { get; private set; } = string.Empty;

        /// <summary>
        /// Returns true if the Tiingo API token has been set.
        /// </summary>
        public static bool IsAuthCodeSet { get; private set; }

        /// <summary>
        /// Sets the Tiingo API token.
        /// </summary>
        /// <param name="authCode">The Tiingo API token</param>
        public static void SetAuthCode(string authCode)
        {
            if (string.IsNullOrWhiteSpace(authCode)) return;

            AuthCode = authCode;
            IsAuthCodeSet = true;
        }
    }
}
