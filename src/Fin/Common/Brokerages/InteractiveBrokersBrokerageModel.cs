            message = null;

            // switch on the currency being bought
            string baseCurrency, quoteCurrency;
            Forex.DecomposeCurrencyPair(currencyPair, out baseCurrency, out quoteCurrency);

            decimal max;
            ForexCurrencyLimits.TryGetValue(baseCurrency, out max);

            var orderIsWithinForexSizeLimits = quantity < max;
            if (!orderIsWithinForexSizeLimits)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "OrderSizeLimit",
                    Invariant($"The maximum allowable order size is {max}{baseCurrency}.")
                );
            }
            return orderIsWithinForexSizeLimits;
        }


        private static readonly IReadOnlyDictionary<string, decimal> ForexCurrencyLimits = new Dictionary<string, decimal>()
        {
            {"USD", 7000000m},
            {"AUD", 6000000m},
            {"CAD", 6000000m},
            {"CHF", 6000000m},
            {"CNH", 40000000m},
            {"CZK", 0m}, // need market price in USD or EUR -- do later when we support
            {"DKK", 35000000m},
            {"EUR", 5000000m},
            {"GBP", 4000000m},
            {"HKD", 50000000m},
            {"HUF", 0m}, // need market price in USD or EUR -- do later when we support
            {"ILS", 0m}, // need market price in USD or EUR -- do later when we support
            {"KRW", 750000000m},
            {"JPY", 550000000m},
            {"MXN", 70000000m},
            {"NOK", 35000000m},
            {"NZD", 8000000m},
            {"RUB", 30000000m},
            {"SEK", 40000000m},
            {"SGD", 8000000m}
        };
    }
}
