using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    public interface IIdentityScope
    {
        string Scope { get; }
        Type IdType { get; }

        void RecoverSkippedIds();

        void CacheNextBlock();
    }



    public interface IIdentityScope<T> : IIdentityScope where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
       

      T GetNextIdentity();

        
    }
}
