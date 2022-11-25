namespace Fin.MemDb;

public partial class MemDb
{
    public int RedisDbIdx { get { return m_Db.RedisDbIdx; } }

    public string TestRedisExecutePing()
    {
        return m_Db.TestRedisExecutePing();
    }
}