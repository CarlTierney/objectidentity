using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    /// <summary>
    /// Defines a factory for creating identity scopes.
    /// </summary>
    /// <remarks>
    /// The identity factory is responsible for creating and initializing identity scopes,
    /// which manage the generation of unique, sequential IDs for specific domains.
    /// </remarks>
    public interface IIdentityFactory
    {
        /// <summary>
        /// Creates an identity scope for the specified type.
        /// </summary>
        /// <typeparam name="T">The type of IDs this scope will generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope, typically corresponding to an entity type or table.</param>
        /// <param name="startingId">Optional starting ID value for the scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <param name="blockSize">The number of IDs to retrieve in each block from the store. Default is 100.</param>
        /// <returns>An initialized identity scope that can generate unique IDs.</returns>
        /// <remarks>
        /// The factory will interact with the underlying identity store to ensure the scope
        /// is properly initialized before returning a scope that can generate IDs.
        /// </remarks>
        IIdentityScope<T> CreateIdentityScope<T>(string? scope, long? startingId = null, long? maxValue = null, int? blockSize = 100)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
            
        /// <summary>
        /// Creates an identity scope asynchronously for the specified type.
        /// </summary>
        /// <typeparam name="T">The type of IDs this scope will generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope, typically corresponding to an entity type or table.</param>
        /// <param name="startingId">Optional starting ID value for the scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <param name="blockSize">The number of IDs to retrieve in each block from the store. Default is 100.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains an initialized identity scope that can generate unique IDs.</returns>
        /// <remarks>
        /// This is the asynchronous version of <see cref="CreateIdentityScope{T}"/> and is recommended
        /// for use in asynchronous applications.
        /// </remarks>
        Task<IIdentityScope<T>> CreateIdentityScopeAsync<T>(string? scope, long? startingId = null, long? maxValue = null, int? blockSize = 100, CancellationToken cancellationToken = default)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    }
}
