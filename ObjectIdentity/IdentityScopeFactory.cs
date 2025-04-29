using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectIdentity
{
    public class IdentityFactory : IIdentityFactory
    {
        private readonly IIdentityStore _identityStore;
        private readonly int _defaultBlockSize;

        public IdentityFactory(IIdentityStore identityScopeInitializer, IOptions<ObjectIdentityOptions> options)
        {
            _identityStore = identityScopeInitializer;
            _defaultBlockSize = options.Value.DefaultBlockSize;
        }

        public IIdentityScope<T> CreateIdentityScope<T>(string scope, long? startingId = null, long? maxValue = null, int? idBlockSize = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var blockFunc = _identityStore.Initialize<T>(scope, startingId, maxValue);
            var idScope = new IdentityScope<T>(idBlockSize ?? _defaultBlockSize, scope, blockFunc);
            return idScope;
        }
    }
}
