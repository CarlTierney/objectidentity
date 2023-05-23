using System;
using System.Collections.Generic;
using System.Text;

namespace Vision.ObjectIdentity
{
    public interface IIdentityBlock<T> where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        T Start { get; set; }
        T End { get; set; }
    }
}
