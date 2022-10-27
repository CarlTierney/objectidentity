using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;


namespace Vision.ObjectIdentity
{
    public class IdentityScopeFactory : IIdentityFactory
    {
        
        private IIdentityScopeInitializer _identityScopeInitializer;
        private object _identityScopeLock = new object();
        private int _defaultBlockSize;

        public IdentityScopeFactory(IIdentityScopeInitializer identityScopeInitializer, int defaultBlocsize = 100)
        {
            _identityScopeInitializer = identityScopeInitializer;
            _defaultBlockSize = defaultBlocsize;
        }

        public IIdentityScope<T> CreateIdentityScope<T>(string scope)
        {
           var blockFunc = _identityScopeInitializer.Initialize<T>(scope);

            var idScope = new IdentityScope<T>(_defaultBlockSize, scope, blockFunc);
            return idScope;

        }
    }
}
