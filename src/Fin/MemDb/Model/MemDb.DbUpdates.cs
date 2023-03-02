using System;
using System.Collections.Generic;
using SqCommon;

namespace Fin.MemDb;

public partial class MemDb
{
    public PortfolioFolder? AddNewPortfolioFolder(User? p_user, string p_name, int p_parentFldId, string p_note)
    {
        string creationTime = DateTime.UtcNow.TohYYYYMMDDHHMMSS(); // DateTime.Now.ToString() => "CTime":"2022-10-13T20:00:00"

        // it seems lock (ItemUpdateLock) is not required. True, many threads can create new items at the same time.
        // But they will be locked and waiting in the memData.AddNewItem() function that is thread safe and has a lock inside.
        // So, every new item has a unique ID. No problem. And writing the half-parallel created new items to the RedisDb can be done parallel.
        // No need to serialize RedisDb writing. The RedisDb itself should be multithread-ready.

        // the m_memData.AddNewPortfolioFolder() does the creation of new ID, the creation of Item and adding to Dictionary. If we separate those tasks to 3 functions, we need new locks
        PortfolioFolder? fld = m_memData.AddNewPortfolioFolder(p_user, p_name, p_parentFldId, creationTime, p_note);
        if (fld == null)
            return null;

        // insert new item into the persistent database (RedisDb, Sql)
        try
        {
            m_Db.AddPortfolioFolder(fld); // can raise System.TimeoutException or others if the RedisDb is offline
        }
        catch (System.Exception) // if error occured in DB writing, revert the transaction back to original state. Do not add the new Folder into MemDb.
        {
            m_memData.RemovePortfolioFolder(fld);
            fld = null;
        }
        return fld;
    }

    public string UpdatePortfolioFolder(int p_id, string p_pfName)
    {
        try
        {
            string errMsg = m_Db.UpdatePortfolioFolder(p_id, p_pfName); // gives back an error message or empty string if everything was OK.
            if (!String.IsNullOrEmpty(errMsg))
                return errMsg;
            // m_memData.UpdatePortfolioFolder(p_id, p_pfName); // Daya - Yet to develop
            return string.Empty;
        }
        catch (System.Exception e)
        {
            return $"Error in MemDb.UpdatePortfolioFolder(): Exception {e.Message}";
        }
    }

    public string DeletePortfolioFolder(int p_id)
    {
        try
        {
            string errMsg = m_Db.DeletePortfolioFolder(p_id); // gives back an error message or empty string if everything was OK.
            if (!String.IsNullOrEmpty(errMsg))
                return errMsg;
            m_memData.DeletePortfolioFolder(p_id);
            return string.Empty;
        }
        catch (System.Exception e)
        {
            return $"Error in MemDb.DeletePortfolioFolder(): Exception {e.Message}";
        }
    }

    public Portfolio? AddNewPortfolio(User? p_user, string p_name, int p_parentFldId, string p_note, string p_currency, string p_userAccess, string p_prtfType)
    {
        string creationTime = DateTime.UtcNow.TohYYYYMMDDHHMMSS(); // DateTime.Now.ToString() => "CTime":"2022-10-13T20:00:00"
        CurrencyId baseCurrency = AssetHelper.gStrToCurrency[p_currency];
        SharedAccess sharedAccess = AssetHelper.gStrToSharedAccess[p_userAccess];
        PortfolioType type = AssetHelper.gStrToPortfolioType[p_prtfType];
        List<User> sharedUsersWith = new();
        Portfolio? prtf = m_memData.AddNewPortfolio(p_user, p_name, p_parentFldId, creationTime, p_note, baseCurrency, type, sharedAccess, sharedUsersWith);
        if (prtf == null)
            return null;
        try
        {
            m_Db.AddPortfolio(prtf); // can raise System.TimeoutException or others if the RedisDb is offline
        }
        catch (System.Exception) // if error occured in DB writing, revert the transaction back to original state. Do not add the new Folder into MemDb.
        {
            m_memData.RemovePortfolio(prtf);
            prtf = null;
        }
        // Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): CreatePortfolio '{creationTime}' '{baseCurrency}' '{type}' '{sharedAccess}' '{sharedUsersWith}' '{p_user}' '{p_name}' '{prtf}'"); // for debugging purpose
        return prtf;
    }

    public string DeletePortfolio(int p_id)
    {
        try
        {
            string errMsg = m_Db.DeletePortfolio(p_id); // gives back an error message or empty string if everything was OK.
            if (!String.IsNullOrEmpty(errMsg))
                return errMsg;

            m_memData.DeletePortfolio(p_id);
            return string.Empty;
        }
        catch (System.Exception e)
        {
            return $"Error in MemDb.DeletePortfolio(): Exception {e.Message}";
        }
    }
}