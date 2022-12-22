namespace QuantConnect.Securities
{
    /// <summary>
    /// Used to seed the security with the correct price
    /// </summary>
    public interface ISecuritySeeder
    {
        /// <summary>
        /// Seed the security
        /// </summary>
        /// <param name="security"><see cref="Security"/> being seeded</param>
        /// <returns>true if the security was seeded, false otherwise</returns>
        bool SeedSecurity(Security security);
    }

    /// <summary>
    /// Provides access to a null implementation for <see cref="ISecuritySeeder"/>
    /// </summary>
    public static class SecuritySeeder
    {
        /// <summary>
        /// Gets an instance of <see cref="ISecuritySeeder"/> that is a no-op
        /// </summary>
        public static readonly ISecuritySeeder Null = new NullSecuritySeeder();

        private sealed class NullSecuritySeeder : ISecuritySeeder
        {
            public bool SeedSecurity(Security security)
            {
                return true;
            }
        }
    }
}
