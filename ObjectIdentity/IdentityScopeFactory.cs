using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectIdentity
{
    public class IdentityScopeFactory : IIdentityFactory
    {
        private readonly IIdentityScopeInitializer _identityScopeInitializer;
        private readonly int _defaultBlockSize;

        public IdentityScopeFactory(IIdentityScopeInitializer identityScopeInitializer, IOptions<ObjectIdentityOptions> options)
        {
            _identityScopeInitializer = identityScopeInitializer;
            _defaultBlockSize = options.Value.DefaultBlockSize;
        }

        public IIdentityScope<T> CreateIdentityScope<T>(string scope, long? startingId = null, long? maxValue = null, int? idBlockSize = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var blockFunc = _identityScopeInitializer.Initialize<T>(scope, startingId, maxValue);
            var idScope = new IdentityScope<T>(idBlockSize ?? _defaultBlockSize, scope, blockFunc);
            return idScope;
        }
    }
}
