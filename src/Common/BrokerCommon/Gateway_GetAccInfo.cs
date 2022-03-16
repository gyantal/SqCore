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
using System.Diagnostics;

namespace BrokerCommon
{


    public partial class Gateway
    {
        List<BrAccSum> m_accSums = new();
        List<BrAccPos> m_accPoss = new();
        string[] m_exclSymbolsArr = Array.Empty<string>();
        private readonly object m_getAccountSummaryLock = new();

        public void AccSumArrived(int p_reqId, string p_tag, string p_value, string p_currency)
        {
            m_accSums.Add(new BrAccSum() { Tag = p_tag, Value = p_value, Currency = p_currency });
        }

        ManualResetEventSlim? m_getAccountSummaryMres;
        public void AccSumEnd(int p_reqId)
        {
            if (m_getAccountSummaryMres != null)
                m_getAccountSummaryMres.Set();  // Sets the state of the event to signaled, which allows one or more threads waiting on the event to proceed.

            // if you don't cancel it, all the data update come every 1 minute, which might be good, because we can give it to user instantenously....
            // However, it would be an unnecessary traffic all the time... So, better to Cancel the data streaming.
            BrokerWrapper.CancelAccountSummary(p_reqId);
        }

        private readonly object m_getAccountPositionsLock = new();
        public void AccPosArrived(string p_account, Contract p_contract, double p_pos, double p_avgCost)
        {
            // 2018-11: EUR cash is coming ONLY on DeBlanzac account, not Main account, neither Agy, which also has many other currencies. Maybe it is only a 'virtual' cash FX position. Assume it is virtual, so ignore it.
            // if (p_contract.SecType == "CASH")
            //     return;
            if (p_pos != 0.0 && !m_exclSymbolsArr.Contains(p_contract.Symbol))   // If a position is 0, it means we just sold it, but IB reports it during that day, because of Realized P&L. However, we don't need that position any more.
                m_accPoss.Add(new BrAccPos(p_contract) { Position = p_pos, AvgCost = p_avgCost });
        }

        ManualResetEventSlim? m_getAccountPosMres;
        public void AccPosEnd()
        {
            if (m_getAccountPosMres != null)
                m_getAccountPosMres.Set();  // Sets the state of the event to signaled, which allows one or more threads waiting on the event to proceed.
        }

        public List<BrAccSum>? GetAccountSums()
        {
            List<BrAccSum>? result = null;
            int accReqId = -1;
            try
            {
                Stopwatch sw1 = Stopwatch.StartNew();
                lock (m_getAccountSummaryLock)          // IB only allows one query at a time, so next client has to wait
                {
                    m_accSums = new List<BrAccSum>(); // delete old values
                    if (m_getAccountSummaryMres == null)
                        m_getAccountSummaryMres = new ManualResetEventSlim(false);  // initialize as unsignaled
                    else
                        m_getAccountSummaryMres.Reset();        // set to unsignaled, which makes thread to block

                    accReqId = BrokerWrapper.ReqAccountSummary();

                    bool wasLightSet = m_getAccountSummaryMres.Wait(5000);     // timeout at 5sec
                    if (!wasLightSet)
                    {
                        Utils.Logger.Error("ReqAccountSummary() ended with timeout error.");
                        // it is dangerous to give back half-ready AccSums (where some fields are missing), so in this case if there was no proper end of it, return null, not the empty or the half-ready list
                        result = null;
                    }
                    else
                        result = m_accSums; // save it before releasing the lock, so other threads will not overwrite the result
                    //m_getAccountSummaryMres.Dispose();    // not necessary. We keep it for the next sessions for faster execution.
                }
                sw1.Stop();
                Utils.Logger.Info($"ReqAccountSummary() ends in {sw1.ElapsedMilliseconds}ms GatewayId: '{this.GatewayId}', Thread Id= {Environment.CurrentManagedThreadId}");
            }
            catch (Exception e)
            {
                Utils.Logger.Error($"ReqAccountSummary() ended with exception: {e.Message}. BrokerYF is only replaced by BrokerIB after the IB connection had been established.");
                return null;
            }
            finally
            {
                if (accReqId != -1)
                    BrokerWrapper.CancelAccountSummary(accReqId);
            }
            return result;
        }

        public List<BrAccPos>? GetAccountPoss(string[] p_exclSymbolsArr)
        {
            List<BrAccPos>? result = null;
            try
            {
                Stopwatch sw2 = Stopwatch.StartNew();
                lock (m_getAccountPositionsLock)          //ReqPositions() doesn't have a reqID, so if we allow multiple threads to do it at the same time, we cannot sort out the output
                {
                    m_accPoss = new List<BrAccPos>();  // delete old values
                    m_exclSymbolsArr = p_exclSymbolsArr;
                    if (m_getAccountPosMres == null)
                        m_getAccountPosMres = new ManualResetEventSlim(false);  // initialize as unsignaled
                    else
                        m_getAccountPosMres.Reset();        // set to unsignaled, which makes the thread to block

                    BrokerWrapper.ReqPositions();
                    bool wasLightSet = m_getAccountPosMres.Wait(10000);     // timeout at 10sec
                    if (!wasLightSet)
                        Utils.Logger.Error("ReqPositions() ended with timeout error.");

                    result = m_accPoss; // save it before releasing the lock, so other threads will not overwrite the result
                }
                sw2.Stop(); // London local DEV client to servers: US-East: 180ms, Dublin: 51-64ms. This is 2x ping time. Linux server will be 5-10ms.
                Utils.Logger.Info($"ReqPositions() ends in {sw2.ElapsedMilliseconds}ms GatewayId: '{this.GatewayId}', Thread Id= {Environment.CurrentManagedThreadId}");
            }
            catch (Exception e)
            {
                Utils.Logger.Error("ReqPositions() ended with exception: " + e.Message);
                return null;
            }
            return result;
        }
    }

}
