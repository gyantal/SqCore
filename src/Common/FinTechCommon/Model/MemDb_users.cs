using System;

namespace FinTechCommon
{
    public partial class MemDb
    {
        public void ReloadUsers()
        {
            m_memDataWlocks.m_usersWlock.Wait();
            try
            {
                User[] users = m_Db.GetUsers();
                m_memData.Users = users;
            }
            finally
            {
                m_memDataWlocks.m_usersWlock.Release();
            }
        }

        public void AddUser(User p_user)
        {
            m_memDataWlocks.m_usersWlock.Wait();
            try
            {
                User[] result = new User[Users.Length + 1];
                Users.CopyTo(result, 0);
                result[Users.Length] = p_user;
                m_memData.Users = result;
                // ToDo: insert it into Redis or Sql DB (within this lock, so until it is stored in DB, no ReloadAllData() can run, which might not be able to find this new insertion)
            }
            finally
            {
                m_memDataWlocks.m_usersWlock.Release();
            }
        }

        public void UpdateUser(User p_userOld, User p_userNew)
        {
            m_memDataWlocks.m_usersWlock.Wait(); // need locks to avoid that many writers modifies the same user at the same time. We don't want to go more granular and having semaphores per user row, so we lock the whole table
            try
            {
                // p_userOld pointer searching in Users array is not good. The p_userOld can come from an old User table that was stored in a client ages ago, 
                // if there was a full DB reload since then. Which happens 3 times daily. So p_userOld pointer might not be presented in the actual Main User table.
                // We have to deep search the User based on properties.
                // instead of == referenceEqual, use contentEqual .Equals()
                int iUser = Array.FindIndex(Users, r => r.Equals(p_userOld));
                if (iUser == -1)    // double check that it is really in the memDB, and give warning Exception if not.
                    throw new Exception("MemDb.UpdateUser(), user to change doesn't exist.");
                Users[iUser] = p_userNew;
                // ToDo: insert it into Redis or Sql DB (within this lock, so until it is stored in DB, no ReloadAllData() can run, which might not be able to find this new insertion)
            }
            finally
            {
                m_memDataWlocks.m_usersWlock.Release();
            }
        }

    }
}