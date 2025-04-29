using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectIdentity
{
    public interface IIdentityFactory
    {
        IIdentityScope<T> CreateIdentityScope<T>(string scope, long? startingId = null, long? maxValue = null, int? blockSize = 100)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    }
}
