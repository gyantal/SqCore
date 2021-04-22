using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;
using System.Threading.Tasks;

namespace FinTechCommon
{
    public partial class MemDb
    {
        public void MirrorProdDb(string p_targetDb)
        {
            m_Db.MirrorProdDb(p_targetDb);
        }

        public void UpsertAssets(string p_targetDb)
        {
            m_Db.UpsertAssets(p_targetDb);
        }

    }

}