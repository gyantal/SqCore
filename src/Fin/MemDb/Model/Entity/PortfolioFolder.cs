using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace Fin.MemDb;

public class PortfolioFolderInDb // PortfolioFolder.Id is not in the JSON, which is the HashEntry.Value. It comes separately from the HashEntry.Key
{
    [JsonPropertyName("User")]
    public int UserId { get; set; } = -1;   // Some folders: SqExperiments, Backtest has UserId = -1, indicating there is no proper user
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ParentFolder")]
    public int ParentFolderId { get; set; } = -1;
    [JsonPropertyName("CTime")]
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public PortfolioFolderInDb()
    {
    }

    public PortfolioFolderInDb(PortfolioFolder prtFld)
    {
        UserId = prtFld.User?.Id ?? -1;
        Name = prtFld.Name;
        ParentFolderId = prtFld.ParentFolderId;
        CreationTime = prtFld.CreationTime;
        Note = prtFld.Note;
    }
}

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public class PortfolioFolder
{
    public int Id { get; set; } = -1;
    public User? User { get; set; } = null;   // Some folders: e.g. SqExperiments, Backtest has UserId = -1, so no user.
    public string Name { get; set; } = string.Empty;
    public int ParentFolderId { get; set; } = -1;
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public PortfolioFolder(int id, PortfolioFolderInDb folderInDb, User[] users)
    {
        Id = id;
        User = users.FirstOrDefault(r => r.Id == folderInDb.UserId);
        Name = folderInDb.Name;
        ParentFolderId = folderInDb.ParentFolderId;
        CreationTime = folderInDb.CreationTime;
        Note = folderInDb.Note;
    }

    public PortfolioFolder(int p_id, User? p_user, string p_name, int p_parentFldId, string p_creationTime, string p_note)
    {
        Id = p_id;
        User = p_user;
        Name = p_name;
        ParentFolderId = p_parentFldId;
        CreationTime = p_creationTime;
        Note = p_note;
    }
}