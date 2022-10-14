using System.Diagnostics;

namespace FinTechCommon;

[DebuggerDisplay("{Id}:Name = {Name}, UserId({UserId})")]
public class PortfolioFolder
{
    public int Id { get; set; } = -1;
    public string Name { get; set; } = string.Empty;
    public int UserId { get; set; } = -1;   // Some folders: SqExperiments, Backtest has UserId = -1, indicating there is no proper user
    public int ParentFolderId { get; set; } = -1;
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

}