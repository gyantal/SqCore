namespace FinTechCommon;

public partial class MemDb
{
    public PortfolioFolder? AddPortfolioFolder(User? p_user, string p_name, int p_parentFldId, string p_creationTime, string p_note)
    {
        PortfolioFolder? fld = m_memData.AddPortfolioFolder(p_user, p_name, p_parentFldId, p_creationTime, p_note);
        if (fld == null)
            return null;

        m_Db.AddPortfolioFolder(fld);
        return fld;
    }
}