using System;
using System.Collections.Generic;
using System.Threading;
using IBApi;
using Utils = SqCommon.Utils;

namespace Fin.BrokerCommon;

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
    struct MktDataProgress
    {
        internal string Name;
        internal int NumQueried { get; set; } = 0;
        internal int NumArrived { get; set; } = 0;
        internal int NumError { get; set; } = 0;
        internal int NumMissing { get { return NumQueried - NumArrived - NumError; } } // nMissing = nQueried - nArrived - nError(arrived)

        internal bool IsAllReceived { get { return (NumArrived + NumError) >= NumQueried; } }

        internal List<string> NotArrivedTickers;
        internal List<string> ErrorArrivedTickers;

        internal MktDataProgress(string p_name, int p_length)
        {
            Name = p_name;
            NotArrivedTickers = new List<string>(p_length);
            ErrorArrivedTickers = new List<string>();
        }

        internal void RegisterTicker(string p_localSymbol)
        {
            NumQueried++;
            NotArrivedTickers.Add(p_localSymbol);
        }

        internal void TickerDataArrived(string p_localSymbol)
        {
            bool wasInListAndRemoved = NotArrivedTickers.Remove(p_localSymbol);
            if (wasInListAndRemoved)
                NumArrived++;
        }

        internal void ErrorArrived(string p_localSymbol)
        {
            if (ErrorArrivedTickers.Contains(p_localSymbol))
                return;
            ErrorArrivedTickers.Add(p_localSymbol);
            NumError++;
        }

        internal void LogIfMissing()
        {
            if (NotArrivedTickers.Count == 0)
                return;
            string msg = $"IB.MktData: Missing {Name}: '{String.Join(",", NotArrivedTickers)}'";
            Utils.Logger.Info(msg);
            Console.WriteLine(msg);
        }
    }

    // 2021-12-07: Case Study 1: Missing estPrice: 'SVXY  220121P00015000'  (a penny-option). Snapshot data. RTH: Regular Trading Hours.
    // ReqMktData(1006) CB: 917.57 ms. close: 0.12
    // ReqMktData(1006) CB: 930.08 ms. bidPrice: -1
    // ReqMktData(1006) CB: 931.57 ms. askPrice: 1
    // Received PriorClose = 0.12. Good. In IB TWS: Bid = NaN, Ask= 1.0, from this, we cannot have EstPrice. IbMarkPrice is 0.11, but snapshot-data has no Mark price, which is fine.
    // >If we do streaming price (instead of snapshot) we could have IbMarkPrice. But for better resource management, this is not that important.
    // These exceptional penny-options are marginal positions. The situation exists because its values are close to 0, so there is no Bid. Nobody wants to buy it, only sell.
    // >If EstPrice is missing, in theory we can estimate it with the Average = Ask/Bid = 0+1.0= 0.50, but that would be a grossly overestimate, as IbMark = 0.11, and PriorClose = 0.12.
    // In this special case (When there is no buying Bid (-1)) fake an EstPrice as Ask/4 that is quite close to IbMark. Even in this cases IB estimates an IbMark and uses that for further calculations

    // 2021-12-07: Case Study 2: Missing priorClose: 'QQQ   220121P00100000' and many others.
    // [with Snapshot]  IB.ReqMktData() #OkEstPrice: 11 in 14,  #OkPriorClose: 6 in 14, #OkDelta: 14 in 14 in 13.184sec.
    // [with Streaming] IB.ReqMktData() #OkEstPrice: 13 in 14,  #OkPriorClose: 14 in 14, #OkDelta: 14 in 14 in 60.723sec.
    // So, streaming and snapshot: no difference. (stick to Snapshot if possible)
    // Only when IbClient is the Linux server. No missing priorClose from IB WinClient.
    // Windows Client: Note: Close and Last comes first.
    // ReqMktData(1010) CB: 1032.34 ms. close: 0
    // ReqMktData(1010) CB: 1035.48 ms. lastPrice: 0.01
    // ReqMktData(1010) CB: 1049.82 ms. bidPrice: -1
    // ReqMktData(1010) CB: 1051.94 ms. askPrice: 0.01
    // But on Linux server, the same program code, results many more missing (only #OkPriorClose: 6 in 14). Most importantly Missing PriorClose!!
    // No Last, no Close on Linux server
    // ReqMktData(1010) CB: 932.09 ms. bidPrice: -1
    // ReqMktData(1010) CB: 932.33 ms. askPrice: 0.01
    // after reboot the Linux machine, the same Linux code works as on Windows.
    // Next time try to restart only TWS, not the whole VM. So, the problem is probably in IbTWS, we might have to restart TWS before RTH daily. Or we have to upgrade TWS version too, hope that they solved it in the new TWS version.
    // But next time it happens: quick solution: restart TWS or the Linux server.

    // 2022-07-06:
    // >Upgrading IB TWS to the latest. Maybe that solves the missing ClosePrice problem?
    // The live running release is IB TWS version: 10.12.2e, 2021-12-29. Only 6 month old.
    // The current stable release is almost the same: Stable: "Version 10.12.2o"
    // So, there is no point to upgrade the Stable.
    // I cannot install the "Latest" "Version 10.16.1k - May 27, 2022", because in 10.15 they redesigned the login, so that will break our automatic login.

    // >Trying to restart only WebServer, or TWS or Linux
    // IB.ReqMktData. #OkEstPr: 8 in 8, #OkPriorCls: 7 in 8, #OkDelta: 8 in 8
    // >1. Restarting TWS
    //  "IB.ReqMktData. #OkEstPr: 8 in 8, #OkPriorCls: 7 in 8, #OkDelta: 8 in 8
    // >2. Restarting Linux ,
    // >First runnig of webserver. "IB.ReqMktData. #OkEstPr: 8 in 8, #OkPriorCls: 7 in 8, #OkDelta: 5 in 8
    // Made it worse. Now, Deltas are missing too.
    // >Second running of the webserver: " #OkEstPr: 8 in 8, #OkPriorCls: 8 in 8, #OkDelta: 8 in 8 in 1.959sec"
    // Hmm. So, it seems to be OK. It means it only took TWS a little bit more time to get those Deltas, but it could get them.
    // >Third run: " IB.ReqMktData. #OkEstPr: 8 in 8, #OkPriorCls: 7 in 8, #OkDelta: 8 in 8
    // Eh. So, the closeprice can be missing, even if we restart Linux. It is random whether we receive it or not from IbTWS.

    // >Conclusion:
    // - the missing closePrice cannot be solved by upgrading IbTWS
    // - the missing closePrice cannot be solved by restarting TWS or restarting Linux.
    // - the SqCore code should be ready for missing closePrice data. Using NaN in MemDb can signal that the number is missing. Don't use 0 or -1.
    // - UI clients should be able to handle missing NaN data. For instance, IbTWS shows an empty cell ("") if Ask, Bid or %Chg data is NaN.

    public bool CollectIbMarketData(MktData[] p_mktDatas, bool p_isNeedOptDelta)
    {
        if (p_mktDatas.Length == 0)
            return true;
        if (m_mainGateway == null)
            return false;

        bool isInRegularUsaTradingHoursNow = Utils.IsInRegularUsaTradingHoursNow();

        // IB pacing restriction. TWS:  50 requests / sec, IB gateway: ~120 requests / sec
        // Another way: we can send the first 50 query instantly, then we have to wait 1 seconds and do it in another burst. That would be better if we have only 49 stocks than Sleeping after each.
        // If Sleep every time, we can query 20 tickers per sec, 100 needs 5 sec. However, if we do a burst of 50 once, second burst of 50 can bring 100 in 2 seconds.

        // try to avoid pacing violation at TWS", the above lousy "sleep" is in milliseconds assures the ReqMktDataStream() happens only 50 times per second
        // int c_IbPacingViolationSleepMs = 1000 / 1000;  // MTS.TWS: first time OK, second time: "Max rate of messages per second has been exceeded:max=50 rec=161", so target 120
        // int c_IbPacingViolationSleepMs = 1000 / 120; // MTS.TWS: first time OK, second time: "Max rate of messages per second has been exceeded:max=50 rec=134", so target 90
        // int c_IbPacingViolationSleepMs = 1000 / 90; // MTS.TWS: first,second,third time OK, fourth time: "Max rate of messages per second has been exceeded:max=50 rec=58", so target 60
        // int c_IbPacingViolationSleepMs = 1000 / 60; // MTS.TWS: first,second,third time OK, fourth time: "Max rate of messages per second has been exceeded:max=50 rec=51", so target 50
        // int c_IbPacingViolationSleepMs = 1000 / 50;     // " Warning: Approaching max rate of 50 messages per second (48)". But if I query it quickly, even with this it can give an exceeded rec=71. So, don't start the next query too quickly.
        int c_IbPacingViolationSleepMs = 1000 / 40; // " Warning: Approaching max rate of 50 messages per second (48)", and I tried to be very quick, but I couldn't do error. Good. Keep this.   Queried: 96; sending time for Est prices and CancelMktData: 2*2380 ms

        // Option QQQ 20220121Put100: its value is very low. Bid=None, Ask = 0.02. No wonder its PriorClose (in TWS) = 0.0. But Ib gives proper 0.0 value 80% of the time, with snapshot data 20% of the time it is not filled and left as NaN.
        // But changing snapshot to streaming and waiting 26sec didn't solve it. Still have: IB.MktData: Missing priorClose: 'ARKG  211217C00094210' (even with streaming). Same problem there. Bid=None, PriorClose in TWS is 0.0.
        // Maybe we should do the same here as TWS. If PriorClose doesn't come, assume it as 0.0. (although do it at higher level of code, not here)
        bool isStreamMktData = false;       // Stream or Snapshot. Prefer snapshot mode if possible because less resources. Although IbMarkPrice works only in stream mode, not in snapshot.
        DateTime startTime = DateTime.UtcNow;
        AutoResetEvent priceOrDeltaTickARE = new(false);    // set it to non-signaled => which means Block
        MktDataProgress progressEstPrice = new("estPrice", p_mktDatas.Length), progressPriorClose = new("priorClose", p_mktDatas.Length), progressDelta = new("delta", p_mktDatas.Length);

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

            progressEstPrice.RegisterTicker(mktData.Contract.LocalSymbol);
            progressPriorClose.RegisterTicker(mktData.Contract.LocalSymbol);
            if (contract.SecType == "OPT")
                progressDelta.RegisterTicker(mktData.Contract.LocalSymbol);

            // TEMP for Debug
            // if (contract.LocalSymbol != "QQQ   230120C00565000") // to skip everything else, but this option
            //     continue;

            Gateway? ibGateway = m_mainGateway;
            // ibGateway = m_gateways.FirstOrDefault(r => r.GatewayId == GatewayId.GyantalMain);    // DEBUG
            if (ibGateway == null)
                return false;
            Thread.Sleep(c_IbPacingViolationSleepMs);    // OK.  "Waiting for RT prices: 396.23 ms. Queried: 96; AllUser_AccPos_WithEstPrices ends in 5277ms".
            int mktDataId = ibGateway.BrokerWrapper.ReqMktDataStream(contract, isStreamMktData ? "221" : string.Empty, !isStreamMktData,  // "221" is the code for MarkPrice. If data is streamed continously and then we ask one snapshot of the same contract, snapshot returns currently, and stream also correctly continues later. As expected.
                (cb_mktDataId, cb_mktDataSubscr, cb_tickType, cb_price) => // MktDataArrived callback
                {
                    Utils.Logger.Trace($"{cb_mktDataSubscr.Contract.Symbol} : {TickType.getField(cb_tickType)}: {cb_price}");
                    // if (cb_mktDataSubscr.Contract.SecType == "OPT" && cb_mktDataSubscr.Contract.Symbol == "SVXY" && cb_mktDataSubscr.Contract.Strike == 15.00)    // for DEBUGGING
                    if (cb_mktDataSubscr.Contract.SecType == "OPT" && cb_mktDataSubscr.Contract.LocalSymbol == "QQQ   230120C00565000") // for DEBUGGING
                        Console.WriteLine($"ReqMktData({cb_mktDataSubscr.MarketDataId}) CB: {(DateTime.UtcNow - startTime).TotalMilliseconds:0.00} ms. {TickType.getField(cb_tickType)}: {cb_price}");

                    if (cb_tickType == TickType.CLOSE) // Prior Close, Previous day Close price.
                    {
                        mktData.PriorClosePrice = cb_price; // should we do priceOrDeltaTickARE.Set(); and wait for all PriorClose as well? Probably.
                        progressPriorClose.TickerDataArrived(mktData.Contract.LocalSymbol);
                    }
                    // 2021-12-01: OTH: ASK = BID = -1, but LAST is given.
                    // Store both IbMarkPrice and LastPrice too. And any of them is fine. Sometimes both are given, but at the weekend only LastPrice is given no IbMarkPrice. So, we cannot rely on that.
                    // However, until we wait for other prices, maybe we got the better MarkPrice. If it is given, use the MarkPrice, otherwise the LastPrice. Store both temporarily.
                    if ((cb_tickType == TickType.MARK_PRICE) || (cb_tickType == TickType.LAST))
                    {
                        if (cb_tickType == TickType.MARK_PRICE)
                            mktData.IbMarkPrice = cb_price;
                        if (cb_tickType == TickType.LAST)
                            mktData.LastTradePrice = cb_price;
                        if (Double.IsNaN(mktData.EstPrice)) // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                        {
                            mktData.EstPrice = cb_price;
                            progressEstPrice.TickerDataArrived(mktData.Contract.LocalSymbol);
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
                            // QQQ   230120C00565000: IbTWS shows: there is no Bid: None (-1 arrives), Ask: 0.04. Mark: 0.01
                            // In the case when one of the AskBid is -1, but the other one is a proper value, let's accept it, and create an estimate by using -1 => 0.
                            // so NOT acceptable = when (Ask = -1 && Bid == -1) at the same time
                            isAskBidAcceptable = !((mktData.AskPrice == -1.0) && (mktData.BidPrice == -1.0));
                            if (isAskBidAcceptable && !isInRegularUsaTradingHoursNow)
                            {
                                // sometimes before premarket: ask: 8.0 Bid: 100,000.01. In that case, don't accept it as a correct AskBid
                                // but BRK.A is "340,045" and legit. So, big values should be accepted if both Ask, Bid is big.
                                // if it is premarket, check that their difference, the AskBid spread is also small. If not, ignore them (and later LastClose will be given back)
                                // NOTE: maybe this should be considered an error, no matter if it is isInRegularUsaTradingHoursNow or not.
                                isAskBidAcceptable = Math.Abs(Math.Abs(mktData.AskPrice) - Math.Abs(mktData.BidPrice)) < 90000;
                            }
                        }
                        if (isAskBidAcceptable)
                        {
                            double pAsk = (mktData.AskPrice < 0.0) ? 0.0 : mktData.AskPrice;    // convert -1 => 0
                            double pBid = (mktData.BidPrice < 0.0) ? 0.0 : mktData.BidPrice;
                            double proposedPrice;
                            if (mktData.BidPrice < 0.0) // == -1, If there is no Bid, only Ask (so nobody wants to buy)
                                proposedPrice = pAsk / 4;   //  in this special case fake an EstPrice as Ask/4 that is quite close to IbMark.
                            else
                                proposedPrice = (pAsk + pBid) / 2.0;
                            if (Double.IsNaN(mktData.EstPrice)) // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                            {
                                mktData.EstPrice = proposedPrice;
                                progressEstPrice.TickerDataArrived(mktData.Contract.LocalSymbol);
                                priceOrDeltaTickARE.Set();
                            }
                            else
                            {
                                mktData.EstPrice = proposedPrice;    // update it with new value
                            }
                        }
                    }
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_errorCode, cb_errorMsg) => // MktDataError callback
                {
                    Utils.Logger.Trace($"Error in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {cb_errorCode}: {cb_errorMsg}");
                    mktData.EstPrice = 0;
                    progressEstPrice.ErrorArrived(mktData.Contract.LocalSymbol);
                    priceOrDeltaTickARE.Set();
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_field, cb_value) => // MktDataTickGeneric callback. (e.g. MarkPrice) Assume this is the last callback for snapshot data. (!Not true for OTC stocks, but we only use this for options) Note sometimes it is not called, only Ask,Bid is coming.
                {
                    // Tick Id:1222, Field: halted, Value: 0
                    // HALTED means the trading is halted. It happens around midnight. OTH.
                    Utils.Logger.Trace($"TickGeneric in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {TickType.getField(cb_field)}: {cb_value}");
                    if (cb_mktDataSubscr.Contract.SecType == "OPT" && cb_mktDataSubscr.Contract.LocalSymbol == "QQQ   230120C00565000") // for DEBUGGING
                        Console.WriteLine($"ReqMktData({cb_mktDataSubscr.MarketDataId}) CB: {(DateTime.UtcNow - startTime).TotalMilliseconds:0.00} ms. {TickType.getField(cb_field)}: {cb_value}");

                    if ((cb_mktDataSubscr.Contract.SecType == "IND") || (cb_mktDataSubscr.Contract.SecType == "STK"))
                    { // do nothing. LastPrice is already filled
                    }
                    else // SecType == "OPT"
                    {
                        // maybe only Ask or only Bid was given (correctly, because they don't exist at all for far OTM options)
                        if (Double.IsNaN(mktData.EstPrice))
                        {
                            double pAsk = Double.IsNaN(mktData.AskPrice) ? 0.0 : mktData.AskPrice;
                            double pBid = Double.IsNaN(mktData.BidPrice) ? 0.0 : mktData.BidPrice;

                            // If Bid is missing (-1), and Ask = 1.0, in theory we can estimate it with the Average = Ask/Bid = 0+1.0= 0.50,
                            // but that would be a grossly overestimate, as IbMark = 0.11, and PriorClose = 0.12.
                            double proposedPrice;
                            if (Double.IsNaN(mktData.BidPrice) || mktData.BidPrice < 0.0) // == -1, If there is no Bid, only Ask (so nobody wants to buy)
                                proposedPrice = pAsk / 4;   //  in this special case fake an EstPrice as Ask/4 that is quite close to IbMark.
                            else
                                proposedPrice = (pAsk + pBid) / 2.0;
                            if (Double.IsNaN(mktData.EstPrice)) // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                            {
                                mktData.EstPrice = proposedPrice;
                                progressEstPrice.TickerDataArrived(mktData.Contract.LocalSymbol);
                                priceOrDeltaTickARE.Set();
                            }
                            else
                                mktData.EstPrice = proposedPrice;    // update it with new value
                        }
                    }
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_type) => // MktDataType callback
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
                        if (Double.IsNaN(mktData.EstPrice)) // only increase the nKnownConIdsPrReadyOk counter once when we turn from NaN to a proper number.
                        {
                            Utils.Logger.Trace($"MarketDataType in ReqMktDataStream(). Filling zero estimation for {cb_mktDataSubscr.Contract.Symbol}");
                            mktData.EstPrice = 0;
                            progressEstPrice.TickerDataArrived(mktData.Contract.LocalSymbol);
                            priceOrDeltaTickARE.Set();
                        }
                    }
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_field, cd_impVol, cb_delta, cb_undPrice) => // MktDataTickOptionComputation callback.
                {
                    // Tick Id:1222, Field: halted, Value: 0
                    // 2021-12-01: OTH: ARKK option. MktDataTickOptionComputation callback is not called at all. Even after waiting for 16 seconds. Maybe it is only called in RTH.
                    Utils.Logger.Trace($"TickOptionComputation in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {TickType.getField(cb_field)}: {cb_delta}, {cb_undPrice}");
                    // Console.WriteLine($"MktDataTickOptionCompu({cb_mktDataSubscr.MarketDataId}) CB: {(DateTime.UtcNow - waitEstPrStartTime).TotalMilliseconds:0.00} ms. {TickType.getField(cb_field)}: Delta: {cb_delta:0.##}");
                    if ((cb_mktDataSubscr.Contract.SecType == "IND") || (cb_mktDataSubscr.Contract.SecType == "STK"))
                    { // do nothing. LastPrice is already filled
                    }
                    else // SecType == "OPT"
                    {
                        // TickOptionComputation() comes 4 times with 4 different IV; the lastly reported Delta seems to be the best.
                        // if (cd_impVol != "1.79769313486232E+308")  = Double.MaxValue, but if that comes, then don't replace the last Delta values
                        // if cb_undPrice = 0.0, then every Greek value is computed wrongly, Gamma =0, Vega = 0, Delta = -1. That is a faulty data given after MOC.
                        if (cb_delta != Double.MaxValue && cb_undPrice != 0.0)
                        {
                            // TickType.MODEL_OPTION is more accurate than TickType.ASK_OPTION, TickType.BID_OPTION, TickType.LAST_OPTION  (those are calculated based on Ask/Bid/Last price)
                            // Accept any Delta at the beginning, but later prefer cb_field = TickType.MODEL_OPTION if it arrives (instead of Ask_option, Bid_option, Last_option)
                            bool useDelta = Double.IsNaN(mktData.IbComputedDelta) || cb_field == TickType.MODEL_OPTION;
                            if (useDelta)
                            {
                                mktData.IbComputedDelta = cb_delta;
                                progressDelta.TickerDataArrived(mktData.Contract.LocalSymbol);
                                priceOrDeltaTickARE.Set();
                            }
                        }
                        if (cd_impVol != Double.MaxValue)
                            mktData.IbComputedImpVol = cd_impVol;
                        if (cb_undPrice != Double.MaxValue && cb_undPrice != 0.0) // after market close, even QQQ option returns this nonsense than UnderlyingPrice = 0 or Double.MaxValue. Ignore that.
                            mktData.IbComputedUndPrice = cb_undPrice;
                    }
                });    // ReqMktDataStream(): as Snapshot, not streaming data
            mktData.MktDataID = mktDataId;
        }

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
        // "a snapshot request will only return available data over the 11 second span; ",  https://interactivebrokers.github.io/tws-api/top_data.html#md_snapshot
        int cMaxTimeout = (isStreamMktData ? 60 : 12) * 4;   // 6*4*250 = 6sec was not enough. Needed another 5sec for option Delta calculation. So, now do 16*4*250=16sec.
        while (iTimeoutCount < cMaxTimeout) // all these checks usually takes 0.1 seconds = 100msec, so do it every time after connection
        {
            bool isOneSignalReceived = priceOrDeltaTickARE.WaitOne(250);  // 250ms wait, max 4 * 12 times.
            if (isOneSignalReceived) // so, it was not a timeout, but a real signal
            {
                var waitingDuration = DateTime.UtcNow - startTime;
                if (waitingDuration.TotalMilliseconds > cMaxTimeout * 250) // OTH: waiting for 8 seconds is not enough to get Option.PriorClose price.
                    break;  // if we wait more than 16 seconds, break the While loop

                if (progressEstPrice.IsAllReceived) // break from while if all is received
                {
                    // local Windoms TWS: "Time until having all EstPrices (and Deltas): "
                    // Only 37 prices: 1409.78 ms,  37 prices + 12 option deltas: 1762.84 ms, so we have to wait extra 300msec if we wait for option Deltas too. Fine. However, after MOC, on TWS Main, it took 11 seconds more to get all the Deltas.
                    if (!p_isNeedOptDelta)
                        break;
                    if (progressDelta.IsAllReceived)
                        break;
                }
            }
            else
            {
                iTimeoutCount++;
                int nMissingEstPrice = 0;
                foreach (var mktData in p_mktDatas)
                {
                    if (Double.IsNaN(mktData.EstPrice)) // These shouldn't be here. These are the Totally Unexpected missing ones or the errors. Estimate missing numbers with zero.
                    {
                        nMissingEstPrice++;
                        Utils.Logger.Trace($"Missing: '{mktData.Contract.LocalSymbol}', {mktData.MktDataID}");
                    }
                }
                Utils.Logger.Trace($"GetAccountsInfo(). RT prices {iTimeoutCount}/{cMaxTimeout}x Timeout after {(DateTime.UtcNow - startTime).TotalMilliseconds} ms. nMissingEsttPrice:{nMissingEstPrice}");
            }
        }
        string logMsg = $"IB.ReqMktData. #OkEstPr: {progressEstPrice.NumArrived} in {progressEstPrice.NumQueried}, #OkPriorCls: {progressPriorClose.NumArrived} in {progressPriorClose.NumQueried}, #OkDelta: {progressDelta.NumArrived} in {progressDelta.NumQueried} in {(DateTime.UtcNow - startTime).TotalSeconds:0.000}sec";
        Utils.Logger.Trace(logMsg);  // RT prices: After MOC:185.5ms, during Market:FirstTime:900-1100ms,Next times:450-600ms, Local development all 58 stocks + options, Queried: 58, ReceivedOk: 57, ReceivedErr: 1, Missing: 0,
        Console.WriteLine(logMsg);
        progressEstPrice.LogIfMissing();
        progressPriorClose.LogIfMissing();
        progressDelta.LogIfMissing();

        // we should cancel even the snapshot mktData from BrokerWrapperIb, because the field m_MktDataSubscription is just growing and growing there.
        DateTime cancelDataStartTime = DateTime.UtcNow;
        foreach (var mktData in p_mktDatas)
        {
            if (mktData.MktDataID != -1)
            {
                if (isStreamMktData) // if snapshot data, then no msg is sent to IB at Cancellation.
                    Thread.Sleep(c_IbPacingViolationSleepMs);
                m_mainGateway.BrokerWrapper.CancelMktData(mktData.MktDataID);
            }
        }
        // string msg3 = $"CancelMktData (!we can do it later) in {(DateTime.UtcNow - cancelDataStartTime).TotalMilliseconds:0.00} ms";
        // Utils.Logger.Trace(msg3);  // RT prices: After MOC:185.5ms, during Market:FirstTime:900-1100ms,Next times:450-600ms, Local development all 58 stocks + options, Queried: 58, ReceivedOk: 57, ReceivedErr: 1, Missing: 0,
        return true;
    }
}