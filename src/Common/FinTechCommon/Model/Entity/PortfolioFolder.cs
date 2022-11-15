using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace FinTechCommon;

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public class PortfolioFolder
{
    public int Id { get; set; } = -1;
    public string Name { get; set; } = string.Empty;

    public User? User { get; set; } = null;   // Some folders: e.g. SqExperiments, Backtest has UserId = -1, so no user.
    public int ParentFolderId { get; set; } = -1;
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public PortfolioFolder(int id, PortfolioFolderInDb folderInDb, User[] users)
    {
        Id = id;
        Name = folderInDb.Name;
        User = users.FirstOrDefault(r => r.Id == folderInDb.UserId);
        ParentFolderId = folderInDb.ParentFolderId;
        CreationTime = folderInDb.CreationTime;
        Note = folderInDb.Note;
    }

}

public class PortfolioFolderInDb   // PortfolioFolderId is not in the JSON, which is the HashEntry.Value. It comes separately from the HashEntry.Key
{
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("User")]
    public int UserId { get; set; } = -1;   // Some folders: SqExperiments, Backtest has UserId = -1, indicating there is no proper user
    [JsonPropertyName("ParentFolder")]
    public int ParentFolderId { get; set; } = -1;
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}