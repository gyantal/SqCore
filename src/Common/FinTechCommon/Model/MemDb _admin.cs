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
        public int RedisDbIdx { get { return m_Db.RedisDbIdx; } }

        public string TestRedisExecutePing()
        {
            return m_Db.TestRedisExecutePing();
        }
        public void DbCopy(int sourceDbIdx, int destDbIdx)
        {
            m_Db.DbCopy(sourceDbIdx, destDbIdx);
        }

        public void UpsertAssets(int destDbIdx)
        {
            m_Db.UpsertAssets(destDbIdx);
        }

    }

}