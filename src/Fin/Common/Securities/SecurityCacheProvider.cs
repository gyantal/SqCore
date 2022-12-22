using System.Collections.Generic;

namespace QuantConnect.Securities
{
    /// <summary>
    /// A helper class that will provide <see cref="SecurityCache"/> instances
    /// </summary>
    /// <remarks>The value of this class and its logic is performance.
    /// This class allows for two different <see cref="Security"/> to share the same
    /// data type cache through different instances of <see cref="SecurityCache"/>.
    /// This is used to directly access custom data types through their underlying</remarks>
    public class SecurityCacheProvider
    {
        private readonly Dictionary<Symbol, List<Symbol>> _relatedSymbols;
        private readonly ISecurityProvider _securityProvider;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="securityProvider">The security provider to use</param>
        public SecurityCacheProvider(ISecurityProvider securityProvider)
        {
            _securityProvider = securityProvider;
            _relatedSymbols = new ();
        }

        /// <summary>
        /// Will return the <see cref="SecurityCache"/> instance to use for a give Symbol.
        /// If the provided Symbol is a custom type which has an underlying we will try to use the
        /// underlying SecurityCache type cache, if the underlying is not present we will keep track
        /// of the custom Symbol in case it is added later.
        /// </summary>
        /// <returns>The cache instance to use</returns>
        public SecurityCache GetSecurityCache(Symbol symbol)
        {
            var securityCache = new SecurityCache();
            // lock just in case but we do not expect this class be used by multiple consumers
            lock (_relatedSymbols)
            {
                if (symbol.SecurityType == SecurityType.Base && symbol.HasUnderlying)
                {
                    var underlyingSecurity = _securityProvider.GetSecurity(symbol.Underlying);
                    if (underlyingSecurity != null)
                    {
                        // we found the underlying, lets use its data type cache
                        SecurityCache.ShareTypeCacheInstance(underlyingSecurity.Cache, securityCache);
                    }
                    else
                    {
                        // we didn't find the underlying, lets keep track of the underlying symbol which might get added in the future.
                        // else when it is added, we would have to go through existing Securities and find any which use it as underlying
                        if (!_relatedSymbols.TryGetValue(symbol.Underlying, out var relatedSymbols))
                        {
                            _relatedSymbols[symbol.Underlying] = relatedSymbols = new List<Symbol>();
                        }
                        relatedSymbols.Add(symbol);
                    }
                }
                else
                {
                    if (_relatedSymbols.Remove(symbol, out var customSymbols))
                    {
                        // if we are here it means we added a symbol which is an underlying of some existing custom symbols
                        foreach (var customSymbol in customSymbols)
                        {
                            var customSecurity = _securityProvider.GetSecurity(customSymbol);
                            if (customSecurity != null)
                            {
                                // we make each existing custom security cache, use the new instance data type cache
                                // note that if any data already existed in the custom cache it will be passed
                                SecurityCache.ShareTypeCacheInstance(securityCache, customSecurity.Cache);
                            }
                        }
                    }
                }
            }

            return securityCache;
        }
    }
}
