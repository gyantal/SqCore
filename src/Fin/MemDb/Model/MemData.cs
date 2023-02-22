using System;
using System.Collections.Generic;
using SqCommon;

namespace Fin.MemDb;

// the pure data MemData class has 2 purposes:
// 1. collect the Data part of MemDb into one smaller entity. Without the MemDb functionality like timers, or Sql/Redis update.
// 2. at UpdateAll data, the changing of all pure data is just a pointer assignment.
// While Writer is swapping users/assets/HistData pointers client can get NewUserData and OldAssetData.
// Without reader locking even careful clients - who don't store pointers - can get inconsistent pointers.
// It can be solved with a global gMemDbUpdateLock, but then clients should use that, and they will forget it.
// This way it is still possible for a client to get m_memData.OldUserData and 1ms later m_memData.NewAssetData (if write happened between them), but
//   - it is less likely, because pointer swap happened much quicker
//   - if reader clients ask GetAssuredConsistentTables() that doesn't require waiting in a Lock, but very fast, just a pointer local copy. Clients should use that.
// Still better to not expose MemData to the outside world, because clients could store its pointers, and GC will not collect old data
// Clients can still store Assets pointers, but it is better if there are 5 old Assets pointers at clients consuming RAM, then if there are 5 old big MemData at clients holding a lot of memory.

// Multithreading of shared resource. Implement Lockfree-Read, Copy-Modify-Swap-Write Pattern described https://stackoverflow.com/questions/10556806/make-a-linked-list-thread-safe/10557130#10557130
// lockObj is necessary, volatile is necessary (because of readers to not have old copies hiding in their CpuCache)
// the lock is a WriterLock only. No need for ReaderLock. For high concurrency.
// Readers of the Users, Assets, DailyHist, Portfolios should keep the pointer locally as the pointer may get swapped out any time

// lock(object) is banned in async function (because lock is intended for very short time.)
// The official way is SemaphoreSlim (although slower, but writing happens very rarely). Or just turn every async function to sync
// if same thread calls SemaphoreSlim.Wait() second time, it will block. It uses a counter mechanism, and it doesn't store which thread acquired the lock already. So, reentry is not possible.
internal class MemData // don't expose to clients.
{
    public volatile User[] Users = Array.Empty<User>();   // writeable: admin might insert a new user from HTML UI

    // Because Writers use the 'Non-locking copy-and-swap-on-write' pattern, before iterating on AssetCache, Readers using foreach() should get a local pointer and iterate on that. Readers can use Linq.Select() or Where() without local pointer though.
    // AssetsCache localAssetCache = MemDb.AssetCache;
    // foreach (Asset item in localAssetCache)
    public volatile AssetsCache AssetsCache = new();  // writable: user might insert a new asset from HTML UI (although this is dangerous, how to propagate it the gSheet Asset replica)
    public volatile CompactFinTimeSeries<SqDateOnly, uint, float, uint> DailyHist = new();

    // Visibility rules for PortfolioFolders:
    // - Normal users don't see other user's PortfolioFolders. They see a virtual folder with their username ('dkodirekka'),
    // a virtual folder 'Shared with me', 'Shared with Anyone', and a virtual folder called 'AllUsers'
    // - Admin users (developers) see all PortfolioFolders of all human users. Each human user (IsHuman) in a virtual folder with their username.
    // And the 'Shared with me', and 'AllUsers" virtual folders are there too.
    public volatile Dictionary<int, PortfolioFolder> PortfolioFolders = new(); // Not Array, because users can create/delete portfolio folders

    // As Portfolios are assets (nesting), we might store portfolios in AssetCache, not separately
    // ---
    // Visibility rules for Portfolios:
    // A portfolio has a 'SharedWith' field. It can be:
    // - "Restricted": the default: means owner and admins can see it.
    // - "OwnerOnly": hidden even from admin users.
    // - "Anyone": totally public. All users can see that in their 'Shared with Anyone' folder.
    // - "drcharmat,jmcharmat,bstanford": a CSV line listing the user.names that the portfolio is shared with. These appear in the 'Shared with me' virtual folder.

    // Normal users don't see other user's Portfolios. They see the shared portfolios in a virtual 'Shared with me' and 'Shared with Anyone' folders.
    // Admin users (developers) see all Portfolios of all users except those that are 'OwnerOnly'. Admin users also see the 'Shared with me' and 'Shared with Anyone' folders similar to regular users.
    // It means that if a Portfolio is shared with them, they might see it in 2 places: in a 'Shared with *' folder and in the folder hierarchy of that user.
    public volatile Dictionary<int, Portfolio> Portfolios = new(); // temporary illustration of a data that will be not only read, but written by SqCore. Portfolios are not necessary here, because they are Assets as well, so they can go to AssetsCache

    // Clients can add new Assets to AssetCache, like NonPersinted Options, or new Portfolios. Other clients enumerate all AssetCache (e.g. reloading HistData in every 2 hours).
    // So a ReaderWriterLock is needed or 'Non-locking copy-and-swap-on-write' is needed.
    // see "C#\Multithread\SharedData, ReaderWriterLock or LockFreeRead.txt"
    // Option 1: ReaderWriterLocks: exactly designed for shared resources (like Database). This is the way if Writing is frequent or RAM is scarce. But I don't like it.
    // - all Readers have to obtain locks all the time. They will forget it, so it will not protect.
    // - But if they do use readerLocks, then that complicates code readibility and therefore future maintenace
    // - it slows down CPU, execution in the readers.
    // - it doesn't solve deadlock problems. Many readers can hold the lock forever and then writers are not able to write, just wait.
    // Option 2: LockFreeReader (Non-locking copy-and-swap-on-write) is the way to go for fast execution. (sporadic double RAM usage). Also colled: Non-locking, or Deep copy-and-swap-write.
    // - It is the fastest for clients. No locks for clients.
    // - less chance for deadlock. (only writers can lock themselves out, but that is rare).
    // disadvantage 1: clients can have stalled pointers stored.
    // disadvantage 2: without reader locking even careful clients - who don't store pointers - can get inconsistent pointers. while Writer is swapping users/assets/HistData pointers client can get NewUserData and OldAssetData.
    public object AssetsUpdateLock = new();    // Many Writers.
    public object UsersUpdateLock = new();
    public object PrFldUpdateLock = new();
    public object PrtfUpdateLock = new();
    // public object DailyHistUpdateLock = new Object();   // not needed, there is only 1 writer thread. We never change its items one by one. We always recreate the whole in every 2 hours and pointer swap, but there is only 1 writer

    public MemData()
    {
    }

    public MemData(User[] newUsers, AssetsCache newAssetsCache, CompactFinTimeSeries<SqDateOnly, uint, float, uint> newDailyHist, Dictionary<int, PortfolioFolder> portfolioFolders, Dictionary<int, Portfolio> portfolios)
    {
        Users = newUsers;
        PortfolioFolders = portfolioFolders;
        Portfolios = portfolios;
        AssetsCache = newAssetsCache;
        DailyHist = newDailyHist;
    }

    // best 'Non-locking copy-and-swap-on-write' implementation:  https://stackoverflow.com/questions/10556806/make-a-linked-list-thread-safe/10557130#10557130
    public void AddToAssetCacheIfMissing(List<Asset> p_newAssets) // Do NOT implement adding single Asset objects. To encourage that callers call in batches. For CPU/RAM efficiency.
    {
        if (p_newAssets.Count == 0)
            return;
        lock (AssetsUpdateLock)
        {
            List<Asset> cloneAssets = new(AssetsCache.Assets);  // shallow copy of the List
            foreach (var newAsset in p_newAssets)
            {
                if (cloneAssets.Exists(r => r.SqTicker == newAsset.SqTicker))
                    continue;
                newAsset.AssetId = AssetsCache.GenerateUniqueAssetId(newAsset);
                cloneAssets.Add(newAsset);
            }
            var newAssetsCache = new AssetsCache(cloneAssets);  // recreate new AssetsCache from the List
            AssetsCache = newAssetsCache;   // switch pointer is atomic operation
        }
    }

    public void AddUser(User p_user)
    {
        lock (UsersUpdateLock)
        {
            User[] result = new User[Users.Length + 1];
            Users.CopyTo(result, 0);
            result[Users.Length] = p_user;
            Users = result; // switch pointer is atomic operation
            // ToDo: insert it into Redis or Sql DB (within this lock, so until it is stored in DB, no ReloadAllData() can run, which might not be able to find this new insertion)
        }
    }

    public PortfolioFolder AddNewPortfolioFolder(User? p_user, string p_name, int p_parentFldId, string p_creationTime, string p_note)
    {
        lock (PrFldUpdateLock)
        {
            // keep the newId calculation logic here, just right before the prFolder creation.
            // Otherwise, we need new locks to prevent that 2 threads creates 2 new Folders with the same Id.
            int maxFldId = -1;
            foreach (var fldId in PortfolioFolders.Keys)
            {
                if (maxFldId < fldId)
                    maxFldId = fldId;
            }
            int newFldId = ++maxFldId;

            PortfolioFolder fld = new (newFldId, p_user, p_name, p_parentFldId, p_creationTime, p_note);
            PortfolioFolders[newFldId] = fld;
            return fld;
        }
    }

    public bool DeletePortfolioFolder(int p_fldId) // method to delete the folder based on fld key
    {
        lock (PrFldUpdateLock)
        {
            return PortfolioFolders.Remove(p_fldId);
        }
    }

    public bool RemovePortfolioFolder(PortfolioFolder p_prFolder) // method to remove the folder based on prtfFolder - need to discuss with George
    {
        lock (PrFldUpdateLock)
        {
            return PortfolioFolders.Remove(p_prFolder.Id);
        }
    }

    public Portfolio AddNewPortfolio(User? p_user, string p_name, int p_parentFldId, string p_creationTime, string p_note, CurrencyId p_currency, PortfolioType p_type, SharedAccess p_sharedAccess, List<User> p_sharedUsersWith)
    {
         lock (PrtfUpdateLock)
        {
            // keep the newId calculation logic here, just right before the portfolio creation.
            // Otherwise, we need new locks to prevent that 2 threads creates 2 new portfolios with the same Id.
            int maxFldId = -1;
            foreach (var fldId in Portfolios.Keys)
            {
                if (maxFldId < fldId)
                    maxFldId = fldId;
            }
            int newFldId = ++maxFldId;
            Portfolio fld = new (newFldId, p_user, p_name, p_parentFldId, p_creationTime, p_note, p_currency, p_type, p_sharedAccess, p_sharedUsersWith);
            Portfolios[newFldId] = fld;
            return fld;
        }
    }

    public bool DeletePortfolio(int p_prtfId) // method to delete the portfolio based on portoflio key
    {
        lock (PrtfUpdateLock)
        {
            return Portfolios.Remove(p_prtfId);
        }
    }

    public bool RemovePortfolio(Portfolio p_prtf) // method to remove the portfolio based on portfolio key - need to discuss with George
    {
        lock (PrtfUpdateLock)
        {
            return Portfolios.Remove(p_prtf.Id);
        }
    }
}