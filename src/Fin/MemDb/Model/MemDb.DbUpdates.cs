using System;
using System.Collections.Generic;
using Fin.Base;
using SqCommon;

namespace Fin.MemDb;

public partial class MemDb
{
    public string AddOrEditPortfolioFolder(int p_id, User? p_user, string p_name, int p_parentFldId, string p_note, out PortfolioFolder? p_newItem)
    {
        PortfolioFolder? pf = null;
        string errMsg = String.Empty;
        if (p_id == -1) // if id == -1, which is an invalid key in the Db, we create a new Item
        {
            string creationTime = DateTime.UtcNow.TohYYYYMMDDHHMMSS(); // DateTime.Now.ToString() => "CTime":"2022-10-13T20:00:00"

            // it seems lock (ItemUpdateLock) is not required. True, many threads can create new items at the same time.
            // But they will be locked and waiting in the memData.AddNewItem() function that is thread safe and has a lock inside.
            // So, every new item has a unique ID. No problem. And writing the half-parallel created new items to the RedisDb can be done parallel.
            // No need to serialize RedisDb writing. The RedisDb itself should be multithread-ready.

            // the m_memData.AddNewPortfolioFolder() does the creation of new ID, the creation of Item and adding to Dictionary. If we separate those tasks to 3 functions, we need new locks
            pf = m_memData.AddNewPortfolioFolder(p_user, p_name, p_parentFldId, p_note, creationTime);
            if (pf == null)
                errMsg = "Cannot create new item.";
            else
            {
                // insert new item into the persistent database (RedisDb, Sql)
                try
                {
                    m_Db.InsertPortfolioFolder(pf); // can raise System.TimeoutException or others if the RedisDb is offline
                }
                catch (System.Exception) // if error occured in DB writing, revert the transaction back to original state. Do not add the new Folder into MemDb.
                {
                    m_memData.RemovePortfolioFolder(pf);
                    errMsg = "Cannot create new item.";
                    pf = null;
                }
            }
        }
        else // if id is valid, edit the Item
        {
            errMsg = m_Db.UpdatePortfolioFolder(p_id, p_user, p_name, p_parentFldId, p_note); // gives back an error message or empty string if everything was OK.
            if (String.IsNullOrEmpty(errMsg)) // if there is no error in RedisDb operation
                pf = m_memData.EditPortfolioFolder(p_id, p_user, p_name, p_parentFldId, p_note);
        }

        p_newItem = pf;
        return errMsg;
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

    public string AddOrEditPortfolio(int p_id, User? p_user, string p_name, int p_parentFldId, CurrencyId p_currency, PortfolioType p_prtfType, string p_algorithm, string p_algorithmParam, SharedAccess p_userAccess, string p_note, int p_tradeHistoryId, string? p_legacyPrtfName, out Portfolio? p_newItem)
    {
        Portfolio? prtf = null;
        string errMsg = String.Empty;
        List<User> sharedUsersWith = new(); // need to develop (Daya)- capture the user input
        if (p_id == -1) // if id == -1, which is an invalid key in the Db, we create a new Item
        {
            string creationTime = DateTime.UtcNow.TohYYYYMMDDHHMMSS(); // DateTime.Now.ToString() => "CTime":"2022-10-13T20:00:00"
            prtf = m_memData.AddNewPortfolio(p_user, p_name, p_parentFldId, creationTime, p_currency, p_prtfType, p_algorithm, p_algorithmParam, p_userAccess, p_note, sharedUsersWith, p_tradeHistoryId, p_legacyPrtfName);
            if (prtf == null)
                errMsg = "Cannot create new item.";
            else
            {
                // insert new item into the persistent database (RedisDb, Sql)
                try
                {
                    m_Db.InsertPortfolio(prtf); // can raise System.TimeoutException or others if the RedisDb is offline
                }
                catch (System.Exception) // if error occured in DB writing, revert the transaction back to original state. Do not add the new Portfolio into MemDb.
                {
                    m_memData.RemovePortfolio(prtf);
                    errMsg = "Cannot create new item.";
                    prtf = null;
                }
            }
        }
        else // if id is valid, edit the Item
        {
            errMsg = m_Db.UpdatePortfolio(p_id, p_user, p_name, p_parentFldId, p_currency, p_prtfType, p_algorithm, p_algorithmParam, p_userAccess, p_note, sharedUsersWith, p_tradeHistoryId); // gives back an error message or empty string if everything was OK.
            if (String.IsNullOrEmpty(errMsg)) // if there is no error in RedisDb operation
                prtf = m_memData.EditPortfolio(p_id, p_user, p_name, p_parentFldId, p_currency, p_prtfType, p_algorithm, p_algorithmParam, p_userAccess, p_note, sharedUsersWith, p_tradeHistoryId);
        }
        p_newItem = prtf;
        return errMsg;
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