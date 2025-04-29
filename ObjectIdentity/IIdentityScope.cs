using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    public interface IIdentityScope
    {
        string Scope { get; }
        Type IdType { get; }
        
        /// <summary>
        /// Gets the current block size being used for this scope
        /// </summary>
        int CurrentBlockSize { get; }
        
        /// <summary>
        /// Gets the count of available IDs in the current queue
        /// </summary>
        int AvailableIdsCount { get; }

        void RecoverSkippedIds();
        
        /// <summary>
        /// Recover skipped IDs asynchronously
        /// </summary>
        Task RecoverSkippedIdsAsync(CancellationToken cancellationToken = default);

        void CacheNextBlock();
        
        /// <summary>
        /// Asynchronously caches the next block of IDs
        /// </summary>
        Task CacheNextBlockAsync(CancellationToken cancellationToken = default);
    }

    public interface IIdentityScope<T> : IIdentityScope where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Gets the next identity value synchronously
        /// </summary>
        T GetNextIdentity();
        
        /// <summary>
        /// Gets the next identity value asynchronously
        /// </summary>
        Task<T> GetNextIdentityAsync(CancellationToken cancellationToken = default);
    }
}
