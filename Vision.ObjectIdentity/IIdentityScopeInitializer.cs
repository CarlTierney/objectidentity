using System;
using System.Collections.Generic;
using System.Text;

namespace Vision.ObjectIdentity
{
    public interface IIdentityScopeInitializer
    {
        Func<int, List<T>> Initialize<T>(string scope);
    }
}
