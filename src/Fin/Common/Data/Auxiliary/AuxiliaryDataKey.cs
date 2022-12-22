namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Unique definition key for a collection of auxiliary data for a Market and SecurityType
    /// </summary>
    public class AuxiliaryDataKey
    {
        /// <summary>
        /// USA equities market corporate actions key definition
        /// </summary>
        public static AuxiliaryDataKey EquityUsa { get; } = new (QuantConnect.Market.USA, SecurityType.Equity);

        /// <summary>
        /// The market associated with these corporate actions
        /// </summary>
        public string Market { get; }

        /// <summary>
        /// The associated security type
        /// </summary>
        public SecurityType SecurityType { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public AuxiliaryDataKey(string market, SecurityType securityType)
        {
            Market = market;
            SecurityType = securityType;
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Market.GetHashCode();
                return (hashCode*397) ^ SecurityType.GetHashCode();
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param><filterpriority>2</filterpriority>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != GetType()) return false;

            var other = (AuxiliaryDataKey)obj;

            return other.Market == Market
                && other.SecurityType == SecurityType;
        }

        public override string ToString()
        {
            return $"{Market}:{SecurityType}";
        }

        /// <summary>
        /// Helper method to create a new instance from a Symbol
        /// </summary>
        public static AuxiliaryDataKey Create(Symbol symbol) => Create(symbol.HasUnderlying ? symbol.Underlying.ID : symbol.ID);

        /// <summary>
        /// Helper method to create a new instance from a SecurityIdentifier
        /// </summary>
        public static AuxiliaryDataKey Create(SecurityIdentifier securityIdentifier)
        {
            securityIdentifier = securityIdentifier.HasUnderlying ? securityIdentifier.Underlying : securityIdentifier;
            return new AuxiliaryDataKey(securityIdentifier.Market, securityIdentifier.SecurityType);
        }
    }
}
