using IBApi;
using Microsoft.Extensions.Primitives;
using SqCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utils = SqCommon.Utils;

namespace BrokerCommon
{
    public class MktData
    {
        public string SqTicker { get; set; } = string.Empty;
        public uint AssetId { get; set; } = 0;  // AssetId.Invalid = 0;  we cannot store Asset pointers, because FinTechCommon is a higher module than BrokerCommon. Although we can store Objects that point to Assets
        public object? AssetObj { get; set; } = null;
        public Contract Contract { get; set; }
        public double EstPrice { get; set; } = Double.NaN;  // EstPrice can be calculated from Ask/Bid, even if there is no Last Trade price (as options may not trade even 1 contracts for days)
        public int MktDataID { get; set; } = -1;    // for reqMktData
        public double PriorClosePrice { get; set; } = Double.NaN;
        public double AskPrice { get; set; } = Double.NaN;
        public double BidPrice { get; set; } = Double.NaN;
        public double LastTradePrice { get; set; } = Double.NaN;    // Don't call it LastPrice, call it LastTrade Price to emphasize it only exists if there is a trade
        public double IbMarkPrice { get; set; } = Double.NaN;       // streamed (non-snapshot) mode. Usually LastPrice, but if Last is not in Ask-Bid range, then Ask or Bid, whichever makes sense

        public double IbComputedImpVol { get; set; } = Double.NaN;
        public double IbComputedDelta { get; set; } = Double.NaN;
        public double IbComputedUndPrice { get; set; } = Double.NaN;

        public MktData(Contract contract)
        {
            Contract = contract;
        }
    }


    public partial class BrokersWatcher
    {
        public bool CollectIbMarketData(MktData[] p_mktDatas, bool p_isNeedOptDelta)
        {
            // Console.WriteLine($"CollectEstimatedPrices() #{p_mktDatas.Length}");
            if (p_mktDatas.Length == 0)
                return true;
            if (m_mainGateway == null)
                return false;

            bool isInRegularUsaTradingHoursNow = Utils.IsInRegularUsaTradingHoursNow();

            // IB pacing restriction. TWS:  50 requests / sec, IB gateway: ~120 requests / sec
            // Another way: we can send the first 50 query instantly, then we have to wait 1 seconds and do it in another burst. That would be better if we have only 49 stocks than Sleeping after each.
            // If Sleep every time, we can query 20 tickers per sec, 100 needs 5 sec. However, if we do a burst of 50 once, second burst of 50 can bring 100 in 2 seconds.

            // try to avoid pacing violation at TWS", the above lousy "sleep" is in milliseconds assures the ReqMktDataStream() happens only 50 times per second
            //int c_IbPacingViolationSleepMs = 1000 / 1000;  // MTS.TWS: first time OK, second time: "Max rate of messages per second has been exceeded:max=50 rec=161", so target 120
            //int c_IbPacingViolationSleepMs = 1000 / 120; // MTS.TWS: first time OK, second time: "Max rate of messages per second has been exceeded:max=50 rec=134", so target 90
            //int c_IbPacingViolationSleepMs = 1000 / 90; // MTS.TWS: first,second,third time OK, fourth time: "Max rate of messages per second has been exceeded:max=50 rec=58", so target 60
            //int c_IbPacingViolationSleepMs = 1000 / 60; // MTS.TWS: first,second,third time OK, fourth time: "Max rate of messages per second has been exceeded:max=50 rec=51", so target 50
            //int c_IbPacingViolationSleepMs = 1000 / 50;     // " Warning: Approaching max rate of 50 messages per second (48)". But if I query it quickly, even with this it can give an exceeded rec=71. So, don't start the next query too quickly.
            int c_IbPacingViolationSleepMs = 1000 / 40; // " Warning: Approaching max rate of 50 messages per second (48)", and I tried to be very quick, but I couldn't do error. Good. Keep this.   Queried: 96; sending time for Est prices and CancelMktData: 2*2380 ms

            bool isStreamMktData = false;       // Work in snapshot mode. IbMarkPrice works only in stream mode, not in snapshot.
            DateTime waitEstPrStartTime = DateTime.UtcNow;
            AutoResetEvent priceOrDeltaTickARE = new AutoResetEvent(false);    // set it to non-signaled => which means Block
            int nKnownConIdsPrQueried = 0, nKnownConIdsPrReadyOk = 0, nKnownConIdsPrReadyErr = 0;
            int nKnownConIdsDeltaQueried = 0, nKnownConIdsDeltaReadyOk = 0, nKnownConIdsDeltaReadyErr = 0;
            foreach (var mktData in p_mktDatas)
            {
                Contract contract = mktData.Contract;
                if ((contract.Currency != "USD") || (contract.SecType == "WAR") || 
                    (!String.IsNullOrEmpty(contract.Exchange) && (contract.Exchange == "CORPACT" || contract.Exchange == "VALUE" || contract.Exchange == "PINK")))
                {
                    // 2018-12:  Instead of about 150 positions, we decrease it to 96.  Helps in IB pacing restriction (50 msg per sec).
                    // Warrants and "CORPACT" exchange stocks are not important for us. For the sake of being brief and save time, we don't return them to the caller.
                    // CORPACT stocks: INNL.CVR or EDMC.WAR. (a STK). Last ClosePrice comes only after 5 sec. It doesn't worth to wait for it. Skip them.
                    // VALUE stocks: 2 out of 7 Value stocks, KWKAQ, IRGTQ, return no Ask/Bid/Last/IbMarkPrices. Only return LastClose prices 4 seconds after query. It doesn't worth waiting for them.
                    // PINK stocks: 4 out of 7 PINK stocks, RGDXQ, HGGGQ, DXIEF, IMRSQ After MarketClose: only have LastClosePrice, and not LastPrice or IbMarkPrice, so they are missing.
                    // We can accept PINK's LastClose quickly without delay, but better to decrease number of stocks for IB pacing restriction (50 msg per sec).
                    // Currency: EUR, Exchange: SBF: Like GNF, After Market Close only MarketDataType=2(Frozen) came, then nothing. During market hour it worked. Anyway, small position. Ignore.
                    Utils.Logger.Trace($"GetAccountsInfo(). RT prices. Skipping not important troublesome stock: {contract.Symbol} SecTye: {contract.SecType}, Exchange:{contract.Exchange}");
                    mktData.EstPrice = 0.0;
                    continue;
                }

                // If we send contID, we still have to send the Exchange. (stupid, but it works that way). So what is the point of ContractId then if it is not used?
                // Contract.Exchange cannot be left empty, so if it is empty (like with options), fill with SMART
                // Other exchanges, like ARCA, CORPACT, VALUE works fine. Leave them.
                // change NASDAQ only to SMART too, because all NASDAQ stocks (TQQQ, BIB, TLT) return ReqMktDataStream() returns error: ErrCode: 200, Msg: No security definition has been found for the request;
                // examples: ONVO,TLT: NASDAQ, TMF: ARCA, options: null, INNL.CVR: CORPACT
                if (String.IsNullOrEmpty(contract.Exchange) || contract.Exchange == "NASDAQ" || contract.Exchange == "PINK")
                    contract.Exchange = "SMART";

                nKnownConIdsPrQueried++;
                if (contract.SecType == "OPT")
                    nKnownConIdsDeltaQueried++;

                Gateway? ibGateway = m_mainGateway;
                // ibGateway = m_gateways.FirstOrDefault(r => r.GatewayId == GatewayId.GyantalMain);
                if (ibGateway == null)
                    return false;
                Thread.Sleep(c_IbPacingViolationSleepMs);    // OK.  "Waiting for RT prices: 396.23 ms. Queried: 96; AllUser_AccPos_WithEstPrices ends in 5277ms". 
                int mktDataId = ibGateway.BrokerWrapper.ReqMktDataStream(contract, isStreamMktData ? "221" : string.Empty, !isStreamMktData,  // "221" is the code for MarkPrice. If data is streamed continously and then we ask one snapshot of the same contract, snapshot returns currently, and stream also correctly continues later. As expected.
                    (cb_mktDataId, cb_mktDataSubscr, cb_tickType, cb_price) =>  // MktDataArrived callback
                    {
                        Utils.Logger.Trace($"{cb_mktDataSubscr.Contract.Symbol} : {TickType.getField(cb_tickType)}: {cb_price}");
                        // if (cb_mktDataSubscr.Contract.Symbol == "ARKK" && cb_mktDataSubscr.Contract.SecType == "OPT")    // for DEBUGGING
                        //   Console.WriteLine($"ReqMktData({cb_mktDataSubscr.MarketDataId}) CB: {(DateTime.UtcNow - waitEstPrStartTime).TotalMilliseconds:0.00} ms. {TickType.getField(cb_tickType)}: {cb_price}");
                        // Console.WriteLine($"ReqMktData() CB: {(DateTime.UtcNow - waitEstPrStartTime).TotalMilliseconds:0.00} ms. {TickType.getField(cb_tickType)}: {cb_price}");

                        if (cb_tickType == TickType.CLOSE)  // Prior Close, Previous day Close price.
                            mktData.PriorClosePrice = cb_price; // should we do priceOrDeltaTickARE.Set(); and wait for all PriorClose as well? Probably.

                        // 2021-12-01: OTH: ASK = BID = -1, but LAST is given.
                        // Store both IbMarkPrice and LastPrice too. And any of them is fine. Sometimes both are given, but at the weekend only LastPrice is given no IbMarkPrice. So, we cannot rely on that. 
                        // However, until we wait for other prices, maybe we got the better MarkPrice. If it is given, use the MarkPrice, otherwise the LastPrice. Store both temporarily.
                        if ((cb_tickType == TickType.MARK_PRICE) || (cb_tickType == TickType.LAST))
                        {
                            if (cb_tickType == TickType.MARK_PRICE)
                                mktData.IbMarkPrice = cb_price;
                            if (cb_tickType == TickType.LAST)
                                mktData.LastTradePrice = cb_price;
                            if (Double.IsNaN(mktData.EstPrice))    // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                            {
                                mktData.EstPrice = cb_price;
                                nKnownConIdsPrReadyOk++;
                                priceOrDeltaTickARE.Set();
                            }
                            else
                            {
                                if (cb_tickType == TickType.MARK_PRICE) // only MarkPrice can overwrite it, but LastPrice not (once it is filled up)
                                    mktData.EstPrice = cb_price;
                            }
                        }

                        // Discussion: last price would be fine for stocks usually, but midPrice of askBid is needed for Options, because Last can be half a day ago. So, do midPrice in general, except for "IND" where only Last is possible
                        // "STK" or "OPT": MID is the most honest price. LAST may happened 1 hours ago
                        //  far OTM options (VIX, VXX) have only Ask, but no Bid. Estimate missing by zero.
                        // After market hour, even liquid stocks (PM, IBKR) doesn't return Ask,Bid. And we have to wait after the 5 second timeout, when they do TickSnapshotEnd, and just before that they send Trace: IBKR : bidPrice: -1.
                        // so, for stocks. (especially AMC), we should accept LastPrice.
                        // Using LastPrice instead of Ask,Bid for stocks changed that this query of 58 contracts returns in 300msec, instead of 900msec + the possibility of 5second timeout.
                        // That is a very good reason to use the LastPrice.
                        // Or use MarkPrice for stocks. MARK_PRICE (Mark Price (used in TWS P&L computations)): can be calculated.
                        //      The mark price is equal to the LAST price unless:
                        //      Ask < Last - the mark price is equal to the ASK price.
                        //      Bid > Last - the mark price is equal to the BID price.
                        // !!Using IbMarkPrice: query of 58 contracts returns in 130msec, so it is twice as fast as getting LastPrice
                        // For IbMarkPrice. Snapshot gives error. I have to do Stream to get IBMarkPrice. Fine. 
                        //      But with stream, we have to CancelMktData, which is a problem if we do 93 queries, because  IB pacing restriction (TWS: 50 requests/sec)
                        // However, at the weekends or on holidays, IBservers are down, and they don't give IbMarkPrice. So, we have to use LastPrice anyway.
                        // Indices only have LastPrice, no Ask,Bid
                        // Decision: 
                        // 1. for stocks: Don't waith for AskBid, because it may never given or comes only at TickSnapshotEnd after 5 sec. 
                        // one option is to use LastPrice, which is there always. However, it may be too far from a realistic ask-bid MidPrice after Market close.
                        // The best is use IbMarkPrice estimate, because I checked and it is reliably given for all stocks. And it is more honest than LastPrice.
                        // However, because of IB pacing restriction, we should prefer Snapshot, which doesn't allow IbMarkPrice. Let's do this for now.
                        // Note that at the weekend, even liquid stocks (IDX,AFK,PM,IBKR,VBR) in snapshot mode or stream returns nothing. (Just MarketDataType(2first,1later), not even LastPrice, or IbMarkPrice).
                        // In that case, there is nothing to do but return 0. But the question is how can TWS estimate the MktVal then? 
                        // TODO: Maybe TWS uses historicalData (not realtime) then when we don't have RT data, but it would delay the query again. So, skip it for now.
                        // During market hours, which is important we can return the correct data.
                        // 2. for options: IbMarkPrice is not available, LastPrice can be too old, so the only way to use AskBid

                        // AskBid is good for both options and stocks if it is given.
                        // we have to use it even for stocks. TMF stock, even in regular trading hours, on AutoTraderServer: only askBid comes, no LastPr. 
                        // However, on ManualTradingServer (Ireland), it is not a problem. So, we have to use AskBid for stocks for Agy account.
                        // TODO: if both Ask,Bid = -1 (no data), use PrClose price, which is given many times.
                        if ((cb_tickType == TickType.ASK) || (cb_tickType == TickType.BID))
                        {
                            if (cb_tickType == TickType.ASK)
                                mktData.AskPrice = cb_price;
                            if (cb_tickType == TickType.BID)
                                mktData.BidPrice = cb_price;

                            // some stocks have proper LastPrice and MarkPrice, but later ask/bid comes as -1/-1 meaning no ask-bid. And because we round that -1 to 0 properly, we have a proposedPrice = 0.
                            // assure that a '0' proposedPrice will not overwrite a previously correct lastPrice 
                            // sometimes Ask or Bid that comes is -1 (even for stock MVV), which shows that Bid is missing (nobody is willing to buy). For non-liquid options, this is acceptable. Round them to 0, or use LastTradedPrice in that case
                            // But if both ask and bid is -1, then don't accept the price, but wait for more data
                            bool isAskBidAcceptable = (!Double.IsNaN(mktData.AskPrice)) && (!Double.IsNaN(mktData.BidPrice));
                            if (isAskBidAcceptable)
                            {
                                isAskBidAcceptable = (mktData.AskPrice != -1.0) && (mktData.BidPrice != -1.0);
                                if (isAskBidAcceptable && !isInRegularUsaTradingHoursNow)
                                {   // sometimes before premarket: ask: 8.0 Bid: 100,000.01. In that case, don't accept it as a correct AskBid
                                    // but BRK.A is "340,045" and legit. So, big values should be accepted if both Ask, Bid is big.
                                    // if it is premarket, check that their difference, the AskBid spread is also small. If not, ignore them (and later LastClose will be given back)
                                    // NOTE: maybe this should be considered an error, no matter if it is isInRegularUsaTradingHoursNow or not.
                                    isAskBidAcceptable = (Math.Abs(Math.Abs(mktData.AskPrice) - Math.Abs(mktData.BidPrice)) < 90000);
                                }
                            }
                            if (isAskBidAcceptable)
                            {
                                double pAsk = (mktData.AskPrice < 0.0) ? 0.0 : mktData.AskPrice;
                                double pBid = (mktData.BidPrice < 0.0) ? 0.0 : mktData.BidPrice;
                                double proposedPrice = (pAsk + pBid) / 2.0;
                                if (Double.IsNaN(mktData.EstPrice))    // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                                {
                                    mktData.EstPrice = proposedPrice;
                                    nKnownConIdsPrReadyOk++;
                                    priceOrDeltaTickARE.Set();
                                }
                                else
                                {
                                    mktData.EstPrice = proposedPrice;    // update it with new value
                                }
                            }
                        }
                    },
                    (cb_mktDataId, cb_mktDataSubscr, cb_errorCode, cb_errorMsg) =>  // MktDataError callback
                    {
                        Utils.Logger.Trace($"Error in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {cb_errorCode}: {cb_errorMsg}");
                        mktData.EstPrice = 0;
                        nKnownConIdsPrReadyErr++;
                        priceOrDeltaTickARE.Set();
                    },
                    (cb_mktDataId, cb_mktDataSubscr, cb_field, cb_value) => // MktDataTickGeneric callback. (e.g. MarkPrice) Assume this is the last callback for snapshot data. (!Not true for OTC stocks, but we only use this for options) Note sometimes it is not called, only Ask,Bid is coming.
                    {  // Tick Id:1222, Field: halted, Value: 0
                        Utils.Logger.Trace($"TickGeneric in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {TickType.getField(cb_field)}: {cb_value}");
                        if ((cb_mktDataSubscr.Contract.SecType == "IND") || (cb_mktDataSubscr.Contract.SecType == "STK"))
                        { // do nothing. LastPrice is already filled
                        }
                        else // SecType == "OPT"
                        {   // maybe only Ask or only Bid was given (correctly, because they don't exist at all for far OTM options)
                            if (Double.IsNaN(mktData.EstPrice))
                            {
                                double pAsk = (Double.IsNaN(mktData.AskPrice)) ? 0.0 : mktData.AskPrice;
                                double pBid = (Double.IsNaN(mktData.BidPrice)) ? 0.0 : mktData.BidPrice;
                                double proposedPrice = (pAsk + pBid) / 2.0;
                                if (Double.IsNaN(mktData.EstPrice))    // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                                {
                                    mktData.EstPrice = proposedPrice;
                                    nKnownConIdsPrReadyOk++;
                                    priceOrDeltaTickARE.Set();
                                }
                                else
                                    mktData.EstPrice = proposedPrice;    // update it with new value
                            }
                        }
                    },
                    (cb_mktDataId, cb_mktDataSubscr, cb_type) =>    // MktDataType callback
                    {
                        Utils.Logger.Trace($"MarketDataType in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {cb_type}");
                        // TMF, VXX can be Frozen(2) too after market close, or at weekend. It means that sometimes there is no more price data. So, we should signal to clients that don't expect more data. Don't wait.
                        // However, 95% of the cases there is proper market data even in this case
                        // weird notice AfterMarketClose: for TMF, VXX, SVXY stocks: MarketDataType(2) first, then MarketDataType(1), then nothing. Other stocks: only MarketDataType(2), then proper Last,Ask,Bid prices
                        // this may means that we started StreamingDataType(2), but later IB realized it is impossible, so changed it to FrozenHistorical (DataType=1)
                        if (cb_mktDataSubscr.PreviousMktDataType == 2 && cb_type == 1)
                        {
                            // Note that at the weekend, even liquid stocks (IDX,AFK,PM,IBKR,VBR) in snapshot mode or stream returns nothing. (Just MarketDataType(2first,1later), not even LastPrice, or IbMarkPrice).
                            // In that case, there is nothing to do but return 0. But the question is how can TWS estimate the MktVal then? 
                            // TODO: Maybe TWS uses historicalData then.
                            if (Double.IsNaN(mktData.EstPrice))    // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                            {
                                Utils.Logger.Trace($"MarketDataType in ReqMktDataStream(). Filling zero estimation for {cb_mktDataSubscr.Contract.Symbol}");
                                mktData.EstPrice = 0;
                                nKnownConIdsPrReadyOk++;
                                priceOrDeltaTickARE.Set();
                            }
                        }
                    },
                    (cb_mktDataId, cb_mktDataSubscr, cb_field, cd_impVol, cb_delta, cb_undPrice) => // MktDataTickOptionComputation callback.
                    {   // Tick Id:1222, Field: halted, Value: 0
                        // 2021-12-01: OTH: ARKK option. MktDataTickOptionComputation callback is not called at all. Even after waiting for 16 seconds. Maybe it is only called in RTH.
                        Utils.Logger.Trace($"TickOptionComputation in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {TickType.getField(cb_field)}: {cb_delta}, {cb_undPrice}");
                        // Console.WriteLine($"MktDataTickOptionCompu({cb_mktDataSubscr.MarketDataId}) CB: {(DateTime.UtcNow - waitEstPrStartTime).TotalMilliseconds:0.00} ms. {TickType.getField(cb_field)}: Delta: {cb_delta:0.##}");
                        if ((cb_mktDataSubscr.Contract.SecType == "IND") || (cb_mktDataSubscr.Contract.SecType == "STK"))
                        { // do nothing. LastPrice is already filled
                        }
                        else // SecType == "OPT"
                        {  // TickOptionComputation() comes 4 times with 4 different IV; the lastly reported Delta seems to be the best.
                            // if (cd_impVol != "1.79769313486232E+308")  = Double.MaxValue, but if that comes, then don't replace the last Delta values
                            // if cb_undPrice = 0.0, then every Greek value is computed wrongly, Gamma =0, Vega = 0, Delta = -1. That is a faulty data given after MOC.
                            if (cb_delta != Double.MaxValue && cb_undPrice != 0.0)
                            {
                                // TickType.MODEL_OPTION is more accurate than TickType.ASK_OPTION, TickType.BID_OPTION, TickType.LAST_OPTION  (those are calculated based on Ask/Bid/Last price)
                                if (cb_field == TickType.MODEL_OPTION && Double.IsNaN(mktData.IbComputedDelta))    // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                                {
                                    Utils.Logger.Trace($"TickGeneric() callback. Filling zero Delta estimation for {cb_mktDataSubscr.Contract.Symbol}");
                                    mktData.IbComputedDelta = cb_delta;
                                    nKnownConIdsDeltaReadyOk++;
                                    priceOrDeltaTickARE.Set();
                                } else
                                    if (cb_field == TickType.MODEL_OPTION)  // accept any Delta at the beginning, but later prefer cb_field = TickType.MODEL_OPTION if it arrives (instead of Ask_option, Bid_option, Last_option)
                                        mktData.IbComputedDelta = cb_delta;
                            }
                            if (cd_impVol != Double.MaxValue)
                                mktData.IbComputedImpVol = cd_impVol;
                            if (cb_undPrice != Double.MaxValue && cb_undPrice != 0.0) // after market close, even QQQ option returns this nonsense than UnderlyingPrice = 0 or Double.MaxValue. Ignore that.
                                mktData.IbComputedUndPrice = cb_undPrice;
                        }
                    });    // ReqMktDataStream(): as Snapshot, not streaming data
                mktData.MktDataID = mktDataId;
            }
            string msg1 = $"ReqMktData() sending time for Est prices: {(DateTime.UtcNow - waitEstPrStartTime).TotalMilliseconds:0.00} ms. Queried: {nKnownConIdsPrQueried}, ReceivedOk: {nKnownConIdsPrReadyOk}, ReceivedErr: {nKnownConIdsPrReadyErr}, Missing: {nKnownConIdsPrQueried - nKnownConIdsPrReadyOk - nKnownConIdsPrReadyErr}";
            Utils.Logger.Trace(msg1);  // RT prices: After MOC:185.5ms, during Market:FirstTime:900-1100ms,Next times:450-600ms, Local development all 58 stocks + options, Queried: 58, ReceivedOk: 57, ReceivedErr: 1, Missing: 0,  
            // Console.WriteLine(msg1);

            // instead of Thread.Sleep(3000);  // wait until data is here; make it sophisticated.
            // for 23 stocks, 0 options, LocalDevelopment, collecting RT price: 273-300ms
            // 2021-12-01: OTH: ARKK option timing. It takes around 7-8 seconds to get the data. So, in general, wait for 16 seconds max.
            // ReqMktData() CB: 7644 ms. bidPrice: -1
            // ReqMktData() CB: 7646 ms. askPrice: -1
            // ReqMktData() CB: 7648 ms. high: 11.2
            // ReqMktData() CB: 7649 ms. lastPrice: 9.4
            // ReqMktData() CB: 7651 ms. low: 7.5
            // ReqMktData() CB: 7652 ms. close: 9.7
            int iTimeoutCount = 0;
            int cMaxTimeout = 16 * 4;   // 6*4*250 = 6sec was not enough. Needed another 5sec for option Delta calculation. So, now do 16*4*250=16sec.
            while (iTimeoutCount < cMaxTimeout)    // all these checks usually takes 0.1 seconds = 100msec, so do it every time after connection 
            {
                bool isOneSignalReceived = priceOrDeltaTickARE.WaitOne(250);  // 250ms wait, max 4 * 16 times.
                if (isOneSignalReceived)   // so, it was not a timeout, but a real signal
                {
                    var waitingDuration = DateTime.UtcNow - waitEstPrStartTime;
                    if (waitingDuration.TotalMilliseconds > 16000.0)    // OTH: waiting for 8 seconds is not enough to get Option.PriorClose price.
                        break;  // if we wait more than 16 seconds, break the While loop


                    bool isAllPrInfoReceived = ((nKnownConIdsPrReadyOk + nKnownConIdsPrReadyErr) >= nKnownConIdsPrQueried);
                    bool isAllDeltaInfoReceived = ((nKnownConIdsDeltaReadyOk + nKnownConIdsDeltaReadyErr) >= nKnownConIdsDeltaQueried);
                    if (isAllPrInfoReceived)    // break from while if all is received
                    {
                        // local Windoms TWS: "Time until having all EstPrices (and Deltas): "
                        // Only 37 prices: 1409.78 ms,  37 prices + 12 option deltas: 1762.84 ms, so we have to wait extra 300msec if we wait for option Deltas too. Fine. However, after MOC, on TWS Main, it took 11 seconds more to get all the Deltas.
                        if (!p_isNeedOptDelta)
                            break;
                        if (isAllDeltaInfoReceived)
                            break; 
                    }
                }
                else
                {
                    iTimeoutCount++;
                    int nMissingLastPrice = 0;
                    foreach (var mktData in p_mktDatas)
                    {
                        if (Double.IsNaN(mktData.EstPrice))     // These shouldn't be here. These are the Totally Unexpected missing ones or the errors. Estimate missing numbers with zero.
                        {
                            nMissingLastPrice++;
                            Utils.Logger.Trace($"Missing: '{mktData.Contract.LocalSymbol}', {mktData.MktDataID}");
                        }
                    }
                    Utils.Logger.Trace($"GetAccountsInfo(). RT prices {iTimeoutCount}/{cMaxTimeout}x Timeout after {(DateTime.UtcNow - waitEstPrStartTime).TotalMilliseconds} ms. nReceived {nKnownConIdsPrReadyOk + nKnownConIdsPrReadyErr} out of {nKnownConIdsPrQueried}. nMissingLastPrice:{nMissingLastPrice}");
                }
            }
            string msg2 = $"Time until having all EstPrices (and Deltas): {(DateTime.UtcNow - waitEstPrStartTime).TotalMilliseconds:0.00} ms. Pr.Queried: {nKnownConIdsPrQueried}, ReceivedOk: {nKnownConIdsPrReadyOk}, ReceivedErr: {nKnownConIdsPrReadyErr}, Missing: {nKnownConIdsPrQueried - nKnownConIdsPrReadyOk - nKnownConIdsPrReadyErr}, Delta.Queried: {nKnownConIdsDeltaQueried}, ReceivedOk: {nKnownConIdsDeltaReadyOk}, ReceivedErr: {nKnownConIdsDeltaReadyErr}, Missing: {nKnownConIdsDeltaQueried - nKnownConIdsDeltaReadyOk - nKnownConIdsDeltaReadyErr}";
            Utils.Logger.Trace(msg2);  // RT prices: After MOC:185.5ms, during Market:FirstTime:900-1100ms,Next times:450-600ms, Local development all 58 stocks + options, Queried: 58, ReceivedOk: 57, ReceivedErr: 1, Missing: 0,  
            // Console.WriteLine(msg2);
            Console.WriteLine($"IB.ReqMktData() #OkPrice: {nKnownConIdsPrReadyOk} in {nKnownConIdsPrQueried}, #OkDelta: {nKnownConIdsDeltaReadyOk} in {nKnownConIdsDeltaQueried} in {(DateTime.UtcNow - waitEstPrStartTime).TotalSeconds:0.000}sec.");

            // we should cancel even the snapshot mktData from BrokerWrapperIb, because the field m_MktDataSubscription is just growing and growing there.
            DateTime cancelDataStartTime = DateTime.UtcNow;
            foreach (var mktData in p_mktDatas)
            {
                if (mktData.MktDataID != -1)
                {
                    if (isStreamMktData)    // if snapshot data, then no msg is sent to IB at Cancellation.
                        Thread.Sleep(c_IbPacingViolationSleepMs);
                    m_mainGateway.BrokerWrapper.CancelMktData(mktData.MktDataID);
                }
            }
            string msg3 = $"CancelMktData (!we can do it later) in {(DateTime.UtcNow - cancelDataStartTime).TotalMilliseconds:0.00} ms";
            Utils.Logger.Trace(msg3);  // RT prices: After MOC:185.5ms, during Market:FirstTime:900-1100ms,Next times:450-600ms, Local development all 58 stocks + options, Queried: 58, ReceivedOk: 57, ReceivedErr: 1, Missing: 0,  
            // Console.WriteLine(msg3);
            return true;
        }

    }
}
