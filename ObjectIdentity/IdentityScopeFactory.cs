using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;


namespace ObjectIdentity
{
    public class IdentityScopeFactory : IIdentityFactory
    {
        
        private IIdentityScopeInitializer _identityScopeInitializer;
        private object _identityScopeLock = new object();
        

        public IdentityScopeFactory(IIdentityScopeInitializer identityScopeInitializer)
        {
            _identityScopeInitializer = identityScopeInitializer;
            
        }

        public IIdentityScope<T> CreateIdentityScope<T>(string scope, long? startingId = null, long? maxValue = null, int? idBlockSize = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var blockFunc = _identityScopeInitializer.Initialize<T>(scope, startingId, maxValue);

            var idScope = new IdentityScope<T>(idBlockSize??100,scope, blockFunc);
            return idScope;

        }
    }
}
