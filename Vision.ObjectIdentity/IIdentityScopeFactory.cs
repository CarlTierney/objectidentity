using System;
using System.Collections.Generic;
using System.Text;

namespace Vision.ObjectIdentity
{
    public interface IIdentityFactory
    {
        IIdentityScope<T> CreateIdentityScope<T>(string scope, long? startingId = null, long? maxValue = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    }
}
