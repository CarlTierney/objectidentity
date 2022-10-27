using System;
using System.Collections.Generic;
using System.Text;

namespace Vision.ObjectIdentity
{
    public interface IIdentityFactory
    {
        IIdentityScope<T> CreateIdentityScope<T>(string scope);
    }
}
