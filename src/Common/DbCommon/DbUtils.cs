using StackExchange.Redis;

namespace DbCommon;

public static partial class DbUtils
{
    public static bool IsRedisAllHashEqual(HashEntry[]? p_arr1, HashEntry[]? p_arr2)
    {
        if (p_arr1 == null)
            return p_arr2 == null;

        if (p_arr2 == null) // here we already know that p_arr1 != null, because of the previous check
            return false;

        if (p_arr1.Length != p_arr2.Length)
            return false;
        for (int i = 0; i < p_arr1.Length; i++)
        {
            if (p_arr1[i].Name != p_arr2[i].Name || p_arr1[i].Value != p_arr2[i].Value) // "==" checks for proper byte to byte equality
                return false;
        }
        return true;
    }
}