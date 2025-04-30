using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    /// <summary>
    /// Defines the core capabilities of an identity scope, which manages identity generation for a specific domain.
    /// </summary>
    /// <remarks>
    /// An identity scope is responsible for retrieving and caching blocks of sequential IDs 
    /// and handing them out to callers in a thread-safe manner.
    /// </remarks>
    public interface IIdentityScope
    {
        /// <summary>
        /// Gets the name of this scope, typically corresponding to an entity type or table name.
        /// </summary>
        string Scope { get; }

        /// <summary>
        /// Gets the data type of IDs this scope generates (e.g., int, long).
        /// </summary>
        Type IdType { get; }
        
        /// <summary>
        /// Gets the current block size being used for this scope.
        /// </summary>
        /// <remarks>
        /// The block size may adjust dynamically based on usage patterns to optimize performance.
        /// </remarks>
        int CurrentBlockSize { get; }
        
        /// <summary>
        /// Gets the count of available IDs in the current queue.
        /// </summary>
        /// <remarks>
        /// This value represents how many IDs are immediately available without requesting a new block.
        /// </remarks>
        int AvailableIdsCount { get; }

        /// <summary>
        /// Recovers IDs that were skipped or not used.
        /// </summary>
        /// <remarks>
        /// This operation helps reuse IDs that were allocated but never used,
        /// which can happen in certain failure scenarios.
        /// </remarks>
        void RecoverSkippedIds();
        
        /// <summary>
        /// Recovers IDs that were skipped or not used asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This operation helps reuse IDs that were allocated but never used,
        /// which can happen in certain failure scenarios.
        /// </remarks>
        Task RecoverSkippedIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Proactively caches the next block of IDs to prevent delays when getting new IDs.
        /// </summary>
        /// <remarks>
        /// Call this method to prefetch IDs in advance of needing them to improve performance
        /// by avoiding blocking operations when requesting new IDs.
        /// </remarks>
        void CacheNextBlock();
        
        /// <summary>
        /// Asynchronously caches the next block of IDs to prevent delays when getting new IDs.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Call this method to prefetch IDs in advance of needing them to improve performance
        /// by avoiding blocking operations when requesting new IDs.
        /// </remarks>
        Task CacheNextBlockAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extends <see cref="IIdentityScope"/> with type-specific identity generation capabilities.
    /// </summary>
    /// <typeparam name="T">The type of IDs this scope generates (e.g., int, long).</typeparam>
    /// <remarks>
    /// This interface provides methods to generate sequential, unique IDs of a specific type.
    /// It handles type safety and provides both synchronous and asynchronous access methods.
    /// </remarks>
    public interface IIdentityScope<T> : IIdentityScope where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Gets the next identity value synchronously.
        /// </summary>
        /// <returns>The next unique ID value of type <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// This method is thread-safe and will block if it needs to fetch a new block of IDs.
        /// For non-blocking behavior, consider using <see cref="CacheNextBlock"/> in advance
        /// or use the asynchronous method <see cref="GetNextIdentityAsync"/>.
        /// </remarks>
        T GetNextIdentity();
        
        /// <summary>
        /// Gets the next identity value asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the next unique ID value.</returns>
        /// <remarks>
        /// This method is the non-blocking alternative to <see cref="GetNextIdentity"/> and is
        /// recommended for use in asynchronous applications to avoid thread blocking.
        /// </remarks>
        Task<T> GetNextIdentityAsync(CancellationToken cancellationToken = default);
    }
}
