using SqCommon;
using IBApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utils = SqCommon.Utils;
using System.Text;

namespace BrokerCommon
{
    // 1. one idea was that if there is no LastPrice, give back the ClosePrice, so we always give back something. 
    // However it doesn't work, as for Indices, even ClosePrice is not received. Nothing.
    // so for Indices, it is ALWAYS possible that we have no data at all, so we have to give back nothing.
    //02-21 08:19:52.459: ***** InitRTP().
    //02-21 08:19:56.738: ^VIX tickID:2
    //02-21 08:19:56.769: ^VXV tickID:3
    //02-21 08:19:56.769: VXX tickID:4
    //02-21 08:19:56.769: XIV tickID:5
    //02-21 08:19:56.769: SPY tickID:6
    //02-21 08:19:56.879: TickPriceCB(): 5/ClosePrice/32.23/False 
    //02-21 08:19:56.879: TickPriceCB(): 4/ClosePrice/42.54/False 
    //02-21 08:19:56.879: TickPriceCB(): 6/ClosePrice/184.10/False 
    //02-21 08:19:56.879: WCF is listening for IIS messages.
    //02-21 08:20:19.796: ***** ExitRTP().

    // 2. Another problem is that the LastChangeTime is simetimes 5 minutes late, because the LastPrice or the LastCalculated Index value didn't change for 5 minutes, but that is still valid
    // However, if a Matlab client shows the Time of the LastChanged, that is 5 minutes late, so users will be puzzled. (how real time is the data). However, the value is still valid, just it didn't change.
    //Decision: Balazs should write both Times to the UI: up-to-dateTime (=queryTime) and LastPriceChangedTime too (so the user can decide if it is good or bad.)
    // >Up-to-dateTime is not required in the output, because it is the queryTime (assuming the service Works properly); if not, service should monitor itself and Give: Error: service doesn't work.
    // >put Ask,Bid too into the output, for later usage, and for safety. AskChangedTimeUtc

    // see this as JSONP JSONP or "JSON with padding" or 'JSON with Prefix', http://en.wikipedia.org/wiki/JSONP , http://bob.ippoli.to/archives/2005/12/05/remote-json-jsonp/
    // This surrounding method call is the P(adding) in JSONP.
    // You can write javascript without semicolon, you only need to insert them if you start a line with a parantesis ( or a bracket [.
    // (In JavaScript semicolon says the line end, and they are immediately flowed by a newline that says the same)

    // the input is the queryString of this 
    //http://hqacompute.cloudapp.net/q/rtp?s=VXX,^VIX,^GSPC,SVXY&f=l 
    //http://hqacompute.cloudapp.net/q/rtp?s=VXX,^VIX,^GSPC,SVXY&f=l&jsonp=MyCallBackFunction
    //string s = @"?s=VXX,^VIX,^GSPC,SVXY,^^^VIX201404,GOOG&f=l&jsonp=MyCallBackFunction";

    public partial class Gateway
    {
        // this service should be implemented using the low-level BrokerWrappers (so if should work, no matter it is IB or YF or GF BrokerWrapper)
        // this will be called multiple times in parallel. So, be careful when to use Shared resources, we need locking
        public string GetRealtimePriceService(string p_input)
        {
            string resultPrefix = "", resultPostfix = "";

            try
            {
                // !!! HACK, temporary: from 2019-01-31 - 2019-05-02, when VXXB was alive. After that VXX is the active again.
                // http://www.snifferquant.com/dac/VixTimer asks for VXX every 20 minutes, which is wrong. VBroker will fail on that. If VXX is asked, we change it to VXXB.
                // TEMPorary. Remove these after Laszlo fixed the query in VixTimer quote
                //p_input = p_input.Replace(",VXX,", ",VXXB");


                // caret(^) is not a valid URL character, so it is encoded to %5E; skip the first '?'  , convert everything to Uppercase, because '%5e', and '%5E' is the same for us
                string input = Uri.UnescapeDataString(p_input.Substring(1));    // change %20 to ' ', and %5E to '^'  , "^VIX" is encoded in the URI as "^%5EVIX"

                string[] inputParams = input.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

                List<Tuple<string, Dictionary<int, PriceAndTime>?, int>> tickerList = new();
                int nTempTickers = 0;
                foreach (var inputParam in inputParams)
                {
                    if (inputParam.StartsWith("S=", StringComparison.CurrentCultureIgnoreCase))    // symbols
                    {
                        string[] tickerArray = inputParam.Substring("S=".Length).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var symbol in tickerArray)
                        {
                            string sqTicker = symbol.ToUpper().Replace("^^", "#");  // Robert's suggestion all Futures tickers #+Underlying, therefore #^VIX, because the underlying is ^VIX

                            Contract contract = VBrokerUtils.ParseYfTickerToContract(sqTicker);
                            Dictionary<int, PriceAndTime> rtPrices;
                            if (sqTicker[0] == '^') // if Index, not stock. Index has only LastPrice and TickType.ClosePrice
                            {
                                rtPrices = new Dictionary<int, PriceAndTime>() {    // we are interested in the following Prices
                                        { TickType.LAST, new PriceAndTime() } };
                            }
                            else
                            {
                                rtPrices = new Dictionary<int, PriceAndTime>() {    // we are interested in the following Prices
                                        { TickType.ASK, new PriceAndTime() },
                                        { TickType.BID, new PriceAndTime() },
                                        //{ TickType.CLOSE, new PriceAndTime() },
                                        { TickType.LAST, new PriceAndTime() } };
                            }
                            if (BrokerWrapper.GetAlreadyStreamedPrice(contract, ref rtPrices))
                            {
                                tickerList.Add(new Tuple<string, Dictionary<int, PriceAndTime>?, int>(sqTicker, rtPrices, -1));  // -1 means: isTemporaryTicker = false, it was permanently subscribed
                            }
                            else
                            {
                                tickerList.Add(new Tuple<string, Dictionary<int, PriceAndTime>?, int>(sqTicker, null, -2));       // -2 means: isTemporaryTicker = true
                                nTempTickers++;
                            }
                        }
                    }

                    if (inputParam.StartsWith("JSONP=", StringComparison.CurrentCultureIgnoreCase))    // symbols
                    {
                        string jsonpPrefix = inputParam.Substring(6);
                        resultPrefix = jsonpPrefix + "(";
                        resultPostfix = ")";  // semicolon; ';' is not required in JS, because of semicolon auto-insertion
                    }
                }



                // 1. if there are tickers without data, subscribe to them, wait for them, get the data
                if (nTempTickers > 0)   // try to enter the Critical section ONLY if it is necessary. Try to not LOCK threads unnecessarily
                {
                    lock (BrokerWrapper)  // RequestMarketData will use gBrokerApi; so Synch it as a Critical section
                    {
                        try
                        {
                            AutoResetEvent priceTickARE = new(false);    // set it to non-signaled => which means Block
                            //priceTickARE.Reset();       // set it to non-signaled => which means Block

                            for (int i = 0; i < tickerList.Count; i++)
                            {
                                if (tickerList[i].Item2 == null) // if it is temporary ticker  (it should be -2, at this point)
                                {
                                    int mktDataId = BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseYfTickerToContract(tickerList[i].Item1), string.Empty, true,
                                        (cb_mktDataId, cb_mktDataSubscr, cb_tickType, cb_price) => {
                                            Utils.Logger.Trace($"{cb_mktDataSubscr.Contract.Symbol} : {cb_tickType}: {cb_price}");
                                            //Console.WriteLine($"{cb_mktDataSubscr.Contract.Symbol} : {cb_tickType}: {cb_price}");  // better not clutter the console
                                            if ((cb_tickType == TickType.LAST) || 
                                                (((cb_tickType == TickType.ASK) || (cb_tickType == TickType.BID)) && (cb_mktDataSubscr.Contract.SecType != "IND"))) // for stocks or for Futures
                                            priceTickARE.Set();
                                        });    // as Snapshot, not streaming data
                                    tickerList[i] = new Tuple<string, Dictionary<int, PriceAndTime>?, int>(tickerList[i].Item1, null, mktDataId);
                                }
                            }

                            // instead of Thread.Sleep(2000);  // wait until data is here; TODO: make it sophisticated later
                            //Thread.Sleep(2000);
                            int iTimeoutCount = 0;
                            DateTime waitStartTime = DateTime.UtcNow;
                            while (iTimeoutCount < 5)    // all these checks usually takes 0.1 seconds = 100msec, so do it every time after connection 
                            {
                                bool isOneSignalReceived = priceTickARE.WaitOne(400);  // 400ms wait, max 5 times.
                                if (isOneSignalReceived)   // so, it was not a timeout, but a real signal
                                {
                                    var waitingDuration = DateTime.UtcNow - waitStartTime;
                                    if (waitingDuration.TotalMilliseconds > 2000.0)
                                        break;  // if we wait more than 2 seconds, break the While loop


                                    bool isAllInfoReceived = true;
                                    for (int i = 0; i < tickerList.Count; i++)
                                    {
                                        if (tickerList[i].Item2 == null) // if it is temporary ticker
                                        {
                                            string sqTicker = tickerList[i].Item1;
                                            int mktDataId = tickerList[i].Item3;
                                            Contract contract = VBrokerUtils.ParseYfTickerToContract(sqTicker);
                                            Dictionary<int, PriceAndTime> rtPrices;
                                            if (sqTicker[0] == '^') // if Index, not stock. Index has only LastPrice and TickType.ClosePrice
                                            {
                                                rtPrices = new Dictionary<int, PriceAndTime>() {    // we are interested in the following Prices
                                                    { TickType.LAST, new PriceAndTime() } };
                                            }
                                            else
                                            {
                                                rtPrices = new Dictionary<int, PriceAndTime>() {    // we are interested in the following Prices
                                                    { TickType.ASK, new PriceAndTime() },
                                                    { TickType.BID, new PriceAndTime() },
                                                    //{ TickType.CLOSE, new PriceAndTime() },
                                                    { TickType.LAST, new PriceAndTime() } };
                                            }
                                            if (BrokerWrapper.GetAlreadyStreamedPrice(contract, ref rtPrices))
                                            {
                                                tickerList[i] = new Tuple<string, Dictionary<int, PriceAndTime>?, int>(sqTicker, rtPrices, mktDataId);
                                            }
                                            else
                                            {
                                                isAllInfoReceived = false;
                                                // break;  don't end the for cycle here. Maybe a ticker (VIX futures) will be never received, because IB user doesn't have subscription. However, all tickers AFTER that ticker should be tried too.
                                            }
                                        }
                                    } // for

                                    if (isAllInfoReceived)
                                    {
                                        break; // break from while if all is received
                                    }
                                }
                                else
                                    iTimeoutCount++;
                            }

                            //Console.WriteLine($"Waiting for RT prices: {(DateTime.UtcNow - waitStartTime).TotalMilliseconds} ms.");  // don't clutter Console
                            Utils.Logger.Trace($"Waiting for RT prices: {(DateTime.UtcNow - waitStartTime).TotalMilliseconds} ms.");


                        }
                        catch (Exception e)
                        {
                            Utils.Logger.Error("GetRealtimePriceService() inner part ended with exception 2: " + e.Message);
                            return resultPrefix + @"{ ""Message"":  ""Exception in WebVBroker app Execute() 2. Exception: " + e.Message + @""" }" + resultPostfix;
                        }
                        finally
                        {
                            foreach (var tickerItem in tickerList)
                            {
                                if (tickerItem.Item3 != -1) // if it was a temporary ticker
                                {
                                    BrokerWrapper.CancelMktData(tickerItem.Item3);
                                }
                            }
                        } // finally
                    } // lock
                } // if nTempTickers

                // 2. Assuming BrokerWrapper.GetAlreadyStreamedPrice() now has all the data
                StringBuilder jsonResultBuilder = new(resultPrefix + "[");
                bool isFirstTickerWrittenToOutput = false;
                foreach (var tickerItem in tickerList)
                {
                    if (isFirstTickerWrittenToOutput)
                        jsonResultBuilder.AppendFormat(",");

                    string sqTicker = tickerItem.Item1;
                    var rtPrices = tickerItem.Item2;
                    bool isTemporaryTicker = (tickerItem.Item3 != -1); // if it is temporary ticker
                    if (rtPrices == null)
                    {
                        jsonResultBuilder.AppendFormat(@"{{""Symbol"":""{0}""}}", sqTicker);  // Data is not given
                    }
                    else
                    {

                        // I wanted to return only Ask/Bid, but because Indices has only LastPrice (no Ask, Bid), I gave up: let's return LastPrice for stocks too
                        //PriceAndTime ask, bid;
                        //isFound = priceInfo.TryGetValue(TickType.AskPrice, out ask);
                        //if (!isFound)
                        //    return @"{ ""Message"":  ""No askprice yet. Maybe later."" }";
                        //isFound = priceInfo.TryGetValue(TickType.BidPrice, out bid);
                        //if (!isFound)
                        //    return @"{ ""Message"":  ""No bidprice yet. Maybe later."" }";
                        //jsonResultBuilder.AppendFormat(@"{{""Symbol"": ""{0}"", ""Ask"": {1}, ""Bid"", {2} }}", ticker, ask.Price, bid.Price);

                        // Warning. I am not sure why this return "" things is here inside the for loop. We may have to eliminate it. or change it to continue.
                        PriceAndTime? ask = null, bid = null;
                        bool isFound = rtPrices.TryGetValue(TickType.LAST, out PriceAndTime? last);
                        if (!isFound)
                            return resultPrefix + @"{ ""Message"":  ""No last price yet. Maybe later."" }" + resultPostfix;    // this didn't happen with the Fixed tickers, as the records are already in the Array at Program start
                        
                        if (sqTicker[0] != '^')
                        {
                            isFound = rtPrices.TryGetValue(TickType.ASK, out ask);
                            if (!isFound)
                                return resultPrefix + @"{ ""Message"":  ""No ask price yet. Maybe later."" }" + resultPostfix;
                            isFound = rtPrices.TryGetValue(TickType.BID, out bid);
                            if (!isFound)
                                return resultPrefix + @"{ ""Message"":  ""No bid price yet. Maybe later."" }" + resultPostfix;
                        }

                        // Robin suggested that don't send "NaN", and 00:00:00 in cases where there is nothing, just send the ticker back
                        // also, if data in memory cache is 10 hours old: say we don't have Realtime price. That is not realtime
                        // for GOOG as a snapshot price, IB gives Ask, Bid instantly, but sometimes it doesn't give Last (as last didn't occur or what). In that case, we want to give back Ask, Bid at least
                        DateTime highestOfAllTime = DateTime.MinValue;
                        if (last != null && last.Time > highestOfAllTime)
                            highestOfAllTime = last.Time;
                        if (ask != null && ask.Time > highestOfAllTime)
                            highestOfAllTime = ask.Time;
                        if (bid != null && bid.Time > highestOfAllTime)
                            highestOfAllTime = bid.Time;
                        if (highestOfAllTime.AddHours(11) < DateTime.UtcNow)    // it was 10 hours at the beginning, but Robert wanted 17 hours; I still disagree, but increased from 10 to 11. It doesn't help with Snapshot prices, because IBGateway doesn't give price for them 10 hours after market close
                        {
                            jsonResultBuilder.AppendFormat(@"{{""Symbol"":""{0}""}}", sqTicker);  // Data is too old.
                        }
                        else
                        {   // last Data is not too old (we could have checked the Ask or Bid data, but Indices have only Last data
                            if (sqTicker[0] == '^') // if Index, not stock. Index has only LastPrice and TickType.ClosePrice
                            {
                                if (last == null || Double.IsNaN(last.Price))
                                    jsonResultBuilder.AppendFormat(@"{{""Symbol"":""{0}""}}", sqTicker);
                                else
                                    jsonResultBuilder.AppendFormat(@"{{""Symbol"":""{0}"",""LastUtc"":""{1:yyyy'-'MM'-'dd'T'HH:mm:ss}"",""Last"":{2},""UtcTimeType"":""{3}""}}", sqTicker, last.Time, last.Price, (isTemporaryTicker) ? "SnapshotTime" : "LastChangedTime");
                            }
                            else
                            {   // stock or Futures, not Index
                                jsonResultBuilder.AppendFormat(@"{{""Symbol"":""{0}""", sqTicker);

                                bool isWrittenAnythingToOutput = false;
                                if (last != null && !Double.IsNaN(last.Price))
                                {
                                    jsonResultBuilder.AppendFormat(@",""LastUtc"":""{0:yyyy'-'MM'-'dd'T'HH:mm:ss}"",""Last"":{1}", last.Time, last.Price);
                                    isWrittenAnythingToOutput = true;
                                }
                                if (bid != null && !Double.IsNaN(bid.Price))
                                {
                                    jsonResultBuilder.AppendFormat(@",""BidUtc"":""{0:yyyy'-'MM'-'dd'T'HH:mm:ss}"",""Bid"":{1}", bid.Time, bid.Price);   // Bid is the smaller than Ask; start with that
                                    isWrittenAnythingToOutput = true;
                                }
                                if (ask != null && !Double.IsNaN(ask.Price))
                                {
                                    jsonResultBuilder.AppendFormat(@",""AskUtc"":""{0:yyyy'-'MM'-'dd'T'HH:mm:ss}"",""Ask"":{1}", ask.Time, ask.Price);
                                    isWrittenAnythingToOutput = true;
                                }

                                if (isWrittenAnythingToOutput)  // if we had no data, there is no point of writing the TimeType
                                {
                                    jsonResultBuilder.AppendFormat(@",""UtcTimeType"":""{0}""", (isTemporaryTicker) ? "SnapshotTime" : "LastChangedTime");
                                }

                                jsonResultBuilder.Append(@"}");
                            }
                        }   // last data is not too old
                    }

                    isFirstTickerWrittenToOutput = true;
                }

                jsonResultBuilder.Append(@"]" + resultPostfix);
                Utils.Logger.Info("GetRealtimePriceService() ended properly: " + jsonResultBuilder.ToString());
                return jsonResultBuilder.ToString();

            }
            catch (Exception e)
            {
                Utils.Logger.Error("GetRealtimePriceService ended with exception: " + e.Message);
                return resultPrefix + @"{ ""Message"":  ""Exception in VBroker app GetRealtimePriceService(). Exception: " + e.Message + @""" }" + resultPostfix;
            }
        }
    }

    }
