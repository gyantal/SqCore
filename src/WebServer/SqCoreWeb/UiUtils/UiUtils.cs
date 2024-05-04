using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fin.MemDb;

namespace SqCoreWeb;
internal class PortfolioItemJs
{
    public int Id { get; set; } = -1;
    [JsonPropertyName("n")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("p")]
    public int ParentFolderId { get; set; } = -1;
    [JsonPropertyName("cTime")]
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    [JsonPropertyName("ouId")]
    public int OwnerUserId { get; set; } = -1;
}

internal class FolderJs : PortfolioItemJs { }

internal class PortfolioJs : PortfolioItemJs
{
    [JsonPropertyName("sAcs")]
    public string SharedAccess { get; set; } = string.Empty;
    [JsonPropertyName("sUsr")]
    public List<User> SharedUsersWith { get; set; } = new();
    [JsonPropertyName("bCur")]
    public string BaseCurrency { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("algo")]
    public string Algorithm { get; set; } = string.Empty;
    [JsonPropertyName("algoP")]
    public string AlgorithmParam { get; set; } = string.Empty;
    [JsonPropertyName("trdHis")]
    public int TradeHistoryId { get; set; } = -1;
}

internal static partial class UiUtils
{
    internal const int gPortfolioIdOffset = 10000;
    internal const int gNoUserVirtPortfId = -2;

    internal static int GetVirtualParentFldId(User? p_user, int p_realParentFldId)
    {
        int virtualParentFldId = p_realParentFldId;
        if (p_realParentFldId == -1) // if Portfolio doesn't have a parent folder, then it is in the Root (of either the NonUser or a concrete user)
        {
            if (p_user == null) // if the owner is the NoUser
                virtualParentFldId = UiUtils.gNoUserVirtPortfId;
            else
                virtualParentFldId = -1 * p_user.Id; // If there is a proper user, the Virtual FolderID is the -1 * UserId by our convention.
        }
        return virtualParentFldId;
    }

    internal static List<FolderJs> GetPortfMgrFolders(User p_user) // Processing the portfolioFolders based on the visiblity rules
    {
        // Visibility rules for PortfolioFolders:
        // - Normal users don't see other user's PortfolioFolders. They see a virtual folder with their username ('dkodirekka'),
        // a virtual folder 'Shared with me', 'Shared with Anyone', and a virtual folder called 'AllUsers'
        // - Admin users (developers) see all PortfolioFolders of all human users. Each human user (IsHuman) in a virtual folder with their username.
        // And the 'Shared with me', and 'AllUsers" virtual folders are there too.
        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        Dictionary<int, Portfolio>.ValueCollection prtfs = MemDb.gMemDb.Portfolios.Values;
        // add the virtual folders to prtfFldrsToClient
        List<FolderJs> prtfFldrsToClient = new();
        Dictionary<int, User> virtUsrFldsToSend = new();

        if (p_user.IsAdmin)
        {
            foreach (PortfolioFolder pf in prtfFldrs) // iterate over all Folders to filter out those users who don't have any folders at all
            {
                if (pf.User != null)
                    virtUsrFldsToSend[pf.User.Id] = pf.User;
            }

            foreach (Portfolio pf in prtfs) // iterate over all Portfolios to filter out those users who don't have any portfolios at all
            {
                if (pf.User != null)
                    virtUsrFldsToSend[pf.User.Id] = pf.User;
            }
        }
        else
        {
            // send only his(User) virtual folder
            virtUsrFldsToSend[p_user.Id] = p_user;  // we send the user his main virtual folder even if he has no sub folders at all
        }

        foreach (var kvp in virtUsrFldsToSend)
        {
            User user = kvp.Value;
            int ownerUserId = user.Id;
            FolderJs pfAdminUserJs = new() { Id = -1 * user.Id, Name = user.Username, OwnerUserId = ownerUserId };
            prtfFldrsToClient.Add(pfAdminUserJs);
        }

        FolderJs pfSharedWithMeJs = new() { Id = 0, Name = "Shared", OwnerUserId = -1 };
        prtfFldrsToClient.Add(pfSharedWithMeJs);

        FolderJs pfAllUsersJs = new() { Id = gNoUserVirtPortfId, Name = "NoUser",  OwnerUserId = -1 };
        prtfFldrsToClient.Add(pfAllUsersJs);

        foreach (PortfolioFolder pf in prtfFldrs)
        {
            bool isSendToUser = p_user.IsAdmin || (pf.User == null) || (pf.User == p_user); // (pf.User == null) means UserId = -1, which means its intended for All users
            if (!isSendToUser)
                continue;

            int virtualParentFldId = GetVirtualParentFldId(pf.User, pf.ParentFolderId);
            int ownerUserId = pf.User?.Id ?? -1;

            FolderJs pfJs = new() { Id = pf.Id, Name = pf.Name, OwnerUserId = ownerUserId, ParentFolderId = virtualParentFldId };
            prtfFldrsToClient.Add(pfJs);
        }
        return prtfFldrsToClient;
    }

    internal static List<PortfolioJs> GetPortfMgrPortfolios(User p_user)
    {
        Dictionary<int, Portfolio>.ValueCollection prtfs = MemDb.gMemDb.Portfolios.Values;
        List<PortfolioJs> prtfsToClient = new();

        foreach (Portfolio pf in prtfs)
        {
            bool isSendToUser = p_user.IsAdmin || (pf.User == null) || (pf.User == p_user); // (pf.User == null) means UserId = -1, which means its intended for All users
            if (!isSendToUser)
                continue;

            int virtualParentFldId = GetVirtualParentFldId(pf.User, pf.ParentFolderId);
            int ownerUserId = pf.User?.Id ?? -1;

            PortfolioJs pfJs = new() { Id = pf.Id + gPortfolioIdOffset, Name = pf.Name, OwnerUserId = ownerUserId, ParentFolderId = virtualParentFldId, BaseCurrency = pf.BaseCurrency.ToString(), SharedAccess = pf.SharedAccess.ToString(), SharedUsersWith = pf.SharedUsersWith, Type = pf.Type.ToString(), Algorithm = pf.Algorithm, AlgorithmParam = pf.AlgorithmParam, TradeHistoryId = pf.TradeHistoryId, Note = pf.Note };
            prtfsToClient.Add(pfJs);
        }
        return prtfsToClient;
    }

    internal static List<FolderJs> GetPortfMgrFolders(object p_user)
    {
        throw new NotImplementedException();
    }

    internal static PortfolioJs GetPortfolioJs(int id) // Method to retrieve a PortfolioJs object based on its ID
    {
        PortfolioJs prtfToClient = new();
        if (MemDb.gMemDb.Portfolios.TryGetValue(id, out Portfolio? pf))
            prtfToClient = new() { Id = pf.Id, Name = pf.Name, OwnerUserId = pf.User?.Id ?? -1, ParentFolderId = pf.ParentFolderId, BaseCurrency = pf.BaseCurrency.ToString(), SharedAccess = pf.SharedAccess.ToString(), SharedUsersWith = pf.SharedUsersWith, Type = pf.Type.ToString(), Algorithm = pf.Algorithm, AlgorithmParam = pf.AlgorithmParam, TradeHistoryId = pf.TradeHistoryId };

        return prtfToClient;
    }
}