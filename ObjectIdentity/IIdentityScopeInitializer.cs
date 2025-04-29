using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectIdentity
{
    public interface IIdentityScopeInitializer
    {
        Func<int, List<T>> Initialize<T>(string scope, long? startingId = null, long? maxValue = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    }

    // Combined interface for both scope initialization and store operations
    public interface IIdentityScopeStoreInitializer
    {
        
    }
}
