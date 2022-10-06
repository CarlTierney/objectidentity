using System;
using System.Collections.Generic;
using System.Text;

namespace Vision.ObjectIdentity
{
    public interface IIdentityBlock<T>
    {
        T Start { get; set; }
        T End { get; set; }
    }
}
