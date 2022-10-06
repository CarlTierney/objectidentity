using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Vision.ObjectIdentity
{
    public interface IIdentityScope
    {
        string Scope { get; }
        Type IdType { get; }

        Type ForType { get; }

        void RecoverSkippedIds();

        void CacheNextBlock();
    }



    public interface IIdentityScope<TScope, T> : IIdentityScope
    {
       

      T GetNextIdentity();

        
    }
}
