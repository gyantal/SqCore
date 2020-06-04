using System;
using System.Collections.Generic;

namespace FinTechCommon
{

    // Not used. Copied from HqFramework for getting ideas; if we need an in-memory data structure for TickerHistory
    // public interface ITickerProvider
    // {
    //     /// <summary> Returns those inputs that does NOT belong to this provider
    //     /// and prepares the answer for those that do belong to it.
    //     /// See ITickerProvider.Prepare&lt;&gt;() extension for more. </summary>
    //     IEnumerable<AssetId32Bits> Prepare(IEnumerable<AssetId32Bits> p_assets);

    //     /// <summary> Returns null if does not know the answer </summary>
    //     string GetTicker(AssetType p_at, int p_subTableId, DateTime p_timeUtc);

    //     /// <summary> Returns 0 (=default(AssetId32Bits)) if does not know the answer, or p_ticker is empty </summary>
    //     AssetId32Bits ParseTicker(string p_ticker, DateTime p_timeUtc, AssetType p_requested = AssetType.Unknown);
    // }

    public static class AssetHelper
    {
        // This can find both "VOD" (Vodafone) ticker in LSE (in GBP), NYSE (in USD).
        public static List<Asset> GetAllMatchingAssets(this List<Asset> p_assets, string p_ticker, ExchangeId p_primExchangeID = ExchangeId.Unknown, DateTime? p_timeUtc = null)
        {
            if (p_timeUtc != null)
                throw new NotImplementedException();    // use Asset.TickerChanges list for historical queries.

            List<Asset> result = new List<Asset>();
            foreach (var sec in p_assets)
            {
                bool accepted = false;
                if (p_primExchangeID == ExchangeId.Unknown) // if exchange is not known, assume all exchange is good
                {
                    if (sec.LastTicker == p_ticker)
                    {
                        accepted = true;
                    }
                }
                else
                {
                    if (sec.LastTicker == p_ticker && sec.PrimaryExchange == p_primExchangeID)
                    {
                        accepted = true;
                    }
                }

                if (accepted)
                    result.Add(sec);
            }
            return result;
        }
    }

}