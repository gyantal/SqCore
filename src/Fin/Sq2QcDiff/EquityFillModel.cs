
// Based on this fix for LimitFill(): https://github.com/QuantConnect/Lean/commit/8bcd588602d14fce9c2d73a576f737212b56d240#diff-c11e3136ddb1093e1a0089c87afa3fe1a1173ee5ccf23d1556d6a183aa3a3317

public override OrderEvent MarketOnCloseFill(Security asset, MarketOnCloseOrder order)
{
	var utcTime = asset.LocalTime.ConvertToUtc(asset.Exchange.TimeZone);
	var fill = new OrderEvent(order, utcTime, OrderFee.Zero);

	if (order.Status == OrderStatus.Canceled) return fill;

	var localOrderTime = order.Time.ConvertFromUtc(asset.Exchange.TimeZone);
	var nextMarketClose = asset.Exchange.Hours.GetNextMarketClose(localOrderTime, false);

	// wait until market closes after the order time
	if (asset.LocalTime < nextMarketClose)
	{
		return fill;
	}

	// Get the range of prices in the last bar:
	var tradeHigh = 0m;
	var tradeLow = 0m;
	var endTimeUtc = DateTime.MinValue;

	var subscribedTypes = GetSubscribedTypes(asset);

	if (subscribedTypes.Contains(typeof(Tick)))
	{
		var primaryExchangeCode = ((Equity)asset).PrimaryExchange.Code;
		var officialClose = (uint)(TradeConditionFlags.Regular | TradeConditionFlags.OfficialClose);
		var closingPrints = (uint)(TradeConditionFlags.Regular | TradeConditionFlags.ClosingPrints);

		var trades = asset.Cache.GetAll<Tick>()
			.Where(x => x.TickType == TickType.Trade && x.Price > 0)
			.OrderBy(x => x.EndTime).ToList();

		// Get the last valid (non-zero) tick of trade type from an close market
		var tick = trades
			.Where(x => !string.IsNullOrWhiteSpace(x.SaleCondition))
			.LastOrDefault(x => x.ExchangeCode == primaryExchangeCode &&
				(x.ParsedSaleCondition == officialClose || x.ParsedSaleCondition == closingPrints));

		// If there is no OfficialClose or ClosingPrints in the current list of trades,
		// we will wait for the next up to 1 minute before accepting the last tick without flags
		// We will give priority to trade then use quote to get the timestamp
		// If there are only quotes, we will need to test for the tick type before we assign the fill price
		if (tick == null)
		{
			tick = trades.LastOrDefault() ?? asset.Cache.GetAll<Tick>().LastOrDefault();
			if (Parameters.ConfigProvider.GetSubscriptionDataConfigs(asset.Symbol).IsExtendedMarketHours())
			{
				fill.Message = Messages.EquityFillModel.MarketOnCloseFillNoOfficialCloseOrClosingPrintsWithinOneMinute;

				if ((tick?.EndTime - nextMarketClose)?.TotalMinutes < 1)
				{
					return fill;
				}
			}
			else
			{
				fill.Message = Messages.EquityFillModel.MarketOnCloseFillNoOfficialCloseOrClosingPrintsWithoutExtendedMarketHours;
			}

			fill.Message += " " + Messages.EquityFillModel.FilledWithLastTickTypeData(tick);
		}

		if (tick?.TickType == TickType.Trade)
		{
			fill.FillPrice = tick.Price;
		}

		// SqCore Change BEGIN : do not fill on stale data
		foreach (var trade in trades)
		{
			tradeHigh = Math.Max(tradeHigh, trade.Price);
			tradeLow = tradeLow == 0 ? trade.Price : Math.Min(tradeLow, trade.Price);
			endTimeUtc = trade.EndTime.ConvertToUtc(asset.Exchange.TimeZone);
		}
		// SqCore Change END
	}
	// SqCore Change BEGIN : do not fill on stale data
	else if (subscribedTypes.Contains(typeof(TradeBar)))
	{
		var tradeBar = asset.Cache.GetData<TradeBar>();

		if (tradeBar != null)
		{
			tradeHigh = tradeBar.High;
			tradeLow = tradeBar.Low;
			endTimeUtc = tradeBar.EndTime.ConvertToUtc(asset.Exchange.TimeZone);
		}
	}

	// do not fill on stale data
	if (endTimeUtc <= order.Time)
		return fill;
	// SqCore Change END

	// make sure the exchange is open/normal market hours before filling
	// It will return true if the last bar opens before the market closes
	else if (!IsExchangeOpen(asset, false))
	{
		return fill;
	}
	else if (subscribedTypes.Contains(typeof(TradeBar)))
	{
		fill.FillPrice = asset.Cache.GetData<TradeBar>()?.Close ?? 0;
	}
	else
	{
		fill.Message = Messages.EquityFillModel.FilledWithQuoteData(asset);
	}

	// Calculate the model slippage: e.g. 0.01c
	var slip = asset.SlippageModel.GetSlippageApproximation(asset, order);

	var bestEffortMessage = "";

	// If there is no trade information, get the bid or ask, then apply the slippage
	switch (order.Direction)
	{
		case OrderDirection.Buy:
			if (fill.FillPrice == 0)
			{
				fill.FillPrice = GetBestEffortAskPrice(asset, order.Time, out bestEffortMessage);
				fill.Message += bestEffortMessage;
			}

			fill.FillPrice += slip;
			break;
		case OrderDirection.Sell:
			if (fill.FillPrice == 0)
			{
				fill.FillPrice = GetBestEffortBidPrice(asset, order.Time, out bestEffortMessage);
				fill.Message += bestEffortMessage;
			}

			fill.FillPrice -= slip;
			break;
	}

	// assume the order completely filled
	fill.FillQuantity = order.Quantity;
	fill.Status = OrderStatus.Filled;

	return fill;
}