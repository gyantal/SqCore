using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SqCommon
{
    public class PersistedState
    {
        public void CreateOrOpen()
        {
            throw new NotImplementedException();
        }

        public void Save() // Save to file
        {
            throw new NotImplementedException();
        }
    }

    public static class PersistedStateExtensions
    {
        // purpose of this is only that writing "m_state = new SavedState().CreateOrOpenEx();" is possible in one line.
        public static TDerived CreateOrOpenEx<TDerived>(this TDerived p_this)
            where TDerived : PersistedState
        {
            p_this.CreateOrOpen();
            return p_this;
        }
    }
}