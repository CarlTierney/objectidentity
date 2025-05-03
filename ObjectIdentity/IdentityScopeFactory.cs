using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    /// <summary>
    /// Default implementation of <see cref="IIdentityFactory"/> that creates identity scopes using an underlying identity store.
    /// </summary>
    /// <remarks>
    /// The identity factory is responsible for creating and initializing identity scopes,
    /// which manage the generation of unique, sequential IDs for specific domains.
    /// </remarks>
    internal class IdentityFactory : IIdentityFactory
    {
        private readonly IIdentityStore _identityStore;
        private readonly int _defaultBlockSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityFactory"/> class.
        /// </summary>
        /// <param name="identityScopeInitializer">The identity store to use for initializing scopes.</param>
        /// <param name="options">Configuration options for identity generation.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="identityScopeInitializer"/> or <paramref name="options"/> is null.</exception>
        public IdentityFactory(IIdentityStore identityScopeInitializer, IOptions<ObjectIdentityOptions> options)
        {
            _identityStore = identityScopeInitializer ?? throw new ArgumentNullException(nameof(identityScopeInitializer));
            var optionsValue = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _defaultBlockSize = optionsValue.DefaultBlockSize;
        }

        /// <summary>
        /// Creates an identity scope for the specified type.
        /// </summary>
        /// <typeparam name="T">The type of IDs this scope will generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope, typically corresponding to an entity type or table.</param>
        /// <param name="startingId">Optional starting ID value for the scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <param name="idBlockSize">The number of IDs to retrieve in each block from the store. If null, uses the default block size from options.</param>
        /// <returns>An initialized identity scope that can generate unique IDs.</returns>
        /// <remarks>
        /// The factory initializes the scope with the identity store, which ensures the underlying
        /// database objects are created and properly configured.
        /// </remarks>
        public IIdentityScope<T> CreateIdentityScope<T>(string? scope, long? startingId = null, long? maxValue = null, int? idBlockSize = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var blockFunc = _identityStore.Initialize<T>(scope, startingId, maxValue);
            var idScope = new IdentityScope<T>(idBlockSize ?? _defaultBlockSize, scope, blockFunc);
            return idScope;
        }

        /// <summary>
        /// Creates an identity scope asynchronously for the specified type.
        /// </summary>
        /// <typeparam name="T">The type of IDs this scope will generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope, typically corresponding to an entity type or table.</param>
        /// <param name="startingId">Optional starting ID value for the scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <param name="idBlockSize">The number of IDs to retrieve in each block from the store. If null, uses the default block size from options.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains an initialized identity scope that can generate unique IDs.</returns>
        /// <remarks>
        /// This is the asynchronous version of <see cref="CreateIdentityScope{T}"/> and is recommended
        /// for use in asynchronous applications.
        /// </remarks>
        public async Task<IIdentityScope<T>> CreateIdentityScopeAsync<T>(string? scope, long? startingId = null, long? maxValue = null, int? idBlockSize = null, CancellationToken cancellationToken = default)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var blockFunc = await _identityStore.InitializeAsync<T>(scope, startingId, maxValue, cancellationToken);
            var idScope = new IdentityScope<T>(idBlockSize ?? _defaultBlockSize, scope, blockFunc);
            return idScope;
        }
    }
}
