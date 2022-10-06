using System;
using System.Collections.Generic;
using System.Text;

namespace Vision.ObjectIdentity
{
    public interface IIdentityFactory
    {
        IIdentityScope<TScope,T> CreateIdentityScope<TScope, T>();
    }
}
