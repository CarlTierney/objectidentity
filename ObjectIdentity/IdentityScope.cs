using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    /// <summary>
    /// Implements a thread-safe identity scope that efficiently manages and distributes unique IDs.
    /// </summary>
    /// <typeparam name="T">The type of IDs this scope generates (e.g., int, long).</typeparam>
    /// <remarks>
    /// <para>
    /// IdentityScope retrieves blocks of sequential IDs from the underlying storage and efficiently
    /// distributes them to callers without requiring a database call for each ID. It includes features
    /// such as:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Adaptive block sizing based on usage patterns</description></item>
    ///   <item><description>Automatic prefetching to minimize latency</description></item>
    ///   <item><description>Thread-safe operations with minimal locking</description></item>
    ///   <item><description>Performance monitoring and telemetry</description></item>
    ///   <item><description>Support for both synchronous and asynchronous operations</description></item>
    /// </list>
    /// </remarks>
    public class IdentityScope<T> : IIdentityScope<T> where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        private readonly Type _idType;
        private int _currentBlockSize;
        private readonly ConcurrentQueue<T> _availableIds;
        private readonly string? _scope;
        private readonly Func<int, List<T>> _blockFunction;
        private Func<int, Task<List<T>>> _asyncBlockFunction;
        private int _fetchingInProgress;
        private bool _gettingNextBlock;
        private Task? _activeBlockFunction;
        private readonly object _nextBlockLock = new object();
        private readonly double _prefetchThreshold = 0.2; // Fetch new block when queue is at 20% capacity
        
        // Adaptive block size properties
        private readonly int _minBlockSize;
        private readonly int _maxBlockSize;
        private readonly TimeSpan _blockSizeAdjustmentInterval = TimeSpan.FromMinutes(5);
        private DateTime _lastBlockSizeAdjustment = DateTime.UtcNow;
        private int _idsFetchedSinceLastAdjustment = 0;
        private readonly IObjectIdentityTelemetry? _telemetry;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityScope{T}"/> class with a synchronous block function.
        /// </summary>
        /// <param name="blockSize">The initial size of ID blocks to retrieve from storage.</param>
        /// <param name="scope">The name of this identity scope, typically corresponding to an entity type or table.</param>
        /// <param name="blockFunction">A function that retrieves blocks of sequential IDs from storage.</param>
        /// <param name="telemetry">Optional telemetry provider for monitoring and performance tracking.</param>
        /// <remarks>
        /// The <paramref name="blockFunction"/> is called to retrieve blocks of sequential IDs
        /// when the cache of available IDs runs low. It takes a block size parameter and returns
        /// a list of IDs to be distributed to callers.
        /// </remarks>
        public IdentityScope(
            int blockSize,
            string? scope,
            Func<int, List<T>> blockFunction,
            IObjectIdentityTelemetry? telemetry = null
            )
        {
            _idType = typeof(T);
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _currentBlockSize = blockSize > 0 ? blockSize : throw new ArgumentException("Block size must be greater than zero", nameof(blockSize));
            _blockFunction = blockFunction ?? throw new ArgumentNullException(nameof(blockFunction));
            _availableIds = new ConcurrentQueue<T>();
            _telemetry = telemetry;
            
            // Setup adaptive block size parameters
            _minBlockSize = Math.Max(10, blockSize / 10);
            _maxBlockSize = blockSize * 10;
            
            // Create an async function wrapper for the sync block function
            _asyncBlockFunction = async (size) => 
            {
                return await Task.Run(() => _blockFunction(size));
            };
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityScope{T}"/> class with an asynchronous block function.
        /// </summary>
        /// <param name="blockSize">The initial size of ID blocks to retrieve from storage.</param>
        /// <param name="scope">The name of this identity scope, typically corresponding to an entity type or table.</param>
        /// <param name="asyncBlockFunction">An asynchronous function that retrieves blocks of sequential IDs from storage.</param>
        /// <param name="telemetry">Optional telemetry provider for monitoring and performance tracking.</param>
        /// <remarks>
        /// The <paramref name="asyncBlockFunction"/> is called to retrieve blocks of sequential IDs
        /// when the cache of available IDs runs low. It takes a block size parameter and returns
        /// a task that yields a list of IDs to be distributed to callers.
        /// </remarks>
        public IdentityScope(
            int blockSize,
            string? scope,
            Func<int, Task<List<T>>> asyncBlockFunction,
            IObjectIdentityTelemetry? telemetry = null
            )
        {
            _idType = typeof(T);
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _currentBlockSize = blockSize > 0 ? blockSize : throw new ArgumentException("Block size must be greater than zero", nameof(blockSize));
            _asyncBlockFunction = asyncBlockFunction ?? throw new ArgumentNullException(nameof(asyncBlockFunction));
            _availableIds = new ConcurrentQueue<T>();
            _telemetry = telemetry;
            
            // Setup adaptive block size parameters
            _minBlockSize = Math.Max(10, blockSize / 10);
            _maxBlockSize = blockSize * 10;
            
            // Create a sync function wrapper for the async block function
            _blockFunction = (size) => 
            {
                return asyncBlockFunction(size).GetAwaiter().GetResult();
            };
        }

        /// <summary>
        /// Gets the data type of IDs this scope generates (e.g., int, long).
        /// </summary>
        public Type IdType => _idType;

        /// <summary>
        /// Gets the name of this identity scope, typically corresponding to an entity type or table.
        /// </summary>
        public string? Scope => _scope;
        
        /// <summary>
        /// Gets the current block size being used for this scope.
        /// </summary>
        /// <remarks>
        /// The block size may adjust dynamically based on usage patterns to optimize performance.
        /// </remarks>
        public int CurrentBlockSize => _currentBlockSize;
        
        /// <summary>
        /// Gets the count of available IDs in the current queue.
        /// </summary>
        /// <remarks>
        /// This value represents how many IDs are immediately available without requesting a new block.
        /// </remarks>
        public int AvailableIdsCount => _availableIds.Count;

        /// <summary>
        /// Proactively caches the next block of IDs to prevent delays when getting new IDs.
        /// </summary>
        /// <remarks>
        /// Call this method to prefetch IDs in advance of needing them to improve performance
        /// by avoiding blocking operations when requesting new IDs.
        /// </remarks>
        public void CacheNextBlock()
        {
            // Start fetching a new block if needed
            if (_availableIds.Count <= _currentBlockSize * _prefetchThreshold)
            {
                GetNextBlockAsync();
            }
        }
        
        /// <summary>
        /// Asynchronously caches the next block of IDs to prevent delays when getting new IDs.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Call this method to prefetch IDs in advance of needing them to improve performance
        /// by avoiding blocking operations when requesting new IDs.
        /// </remarks>
        public async Task CacheNextBlockAsync(CancellationToken cancellationToken = default)
        {
            // Start fetching a new block if needed
            if (_availableIds.Count <= _currentBlockSize * _prefetchThreshold)
            {
                await GetNextBlockAsyncInternal(cancellationToken);
            }
        }

        /// <summary>
        /// Gets the next identity value synchronously.
        /// </summary>
        /// <returns>The next unique ID value of type <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method is thread-safe and will block if it needs to fetch a new block of IDs.
        /// For non-blocking behavior, consider using <see cref="CacheNextBlock"/> in advance
        /// or use the asynchronous method <see cref="GetNextIdentityAsync"/>.
        /// </para>
        /// <para>
        /// The method also handles adaptive block sizing to optimize performance based on usage patterns.
        /// </para>
        /// </remarks>
        public T GetNextIdentity()
        {
            // Track for adaptive block sizing
            Interlocked.Increment(ref _idsFetchedSinceLastAdjustment);
            
            // Check if we should adjust block size
            if (DateTime.UtcNow - _lastBlockSizeAdjustment > _blockSizeAdjustmentInterval)
            {
                AdjustBlockSize();
            }
            
            using var operation = _telemetry?.StartOperation("GetNextIdentity", _scope);
            
            // Try to get an ID from the queue without locking
            if (_availableIds.TryDequeue(out var id))
            {
                // Use an interlocked compare to avoid locking when possible
                int remainingIds = _availableIds.Count;
                if (remainingIds <= _currentBlockSize * _prefetchThreshold && 
                    Interlocked.CompareExchange(ref _fetchingInProgress, 1, 0) == 0)
                {
                    // We successfully set the flag, now fetch asynchronously
                    _ = Task.Run(async () => 
                    {
                        try
                        {
                            await GetNextBlockAsyncInternal();
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _fetchingInProgress, 0);
                        }
                    });
                }
                
                // Track metrics if telemetry is available
                _telemetry?.TrackMetric("AvailableIds", _availableIds.Count, _scope);
                
                return id;
            }

            // If queue is empty, fetch a block and wait for it
            return GetIdWithBlockFetch();
        }
        
        /// <summary>
        /// Gets the next identity value asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the next unique ID value.</returns>
        /// <remarks>
        /// <para>
        /// This method is the non-blocking alternative to <see cref="GetNextIdentity"/> and is
        /// recommended for use in asynchronous applications to avoid thread blocking.
        /// </para>
        /// <para>
        /// The method also handles adaptive block sizing to optimize performance based on usage patterns.
        /// </para>
        /// </remarks>
        public async Task<T> GetNextIdentityAsync(CancellationToken cancellationToken = default)
        {
            // Track for adaptive block sizing
            Interlocked.Increment(ref _idsFetchedSinceLastAdjustment);
            
            // Check if we should adjust block size
            if (DateTime.UtcNow - _lastBlockSizeAdjustment > _blockSizeAdjustmentInterval)
            {
                AdjustBlockSize();
            }
            
            using var operation = _telemetry?.StartOperation("GetNextIdentityAsync", _scope);
            
            // Try to get an ID from the queue without locking
            if (_availableIds.TryDequeue(out var id))
            {
                // Use an interlocked compare to avoid locking when possible
                int remainingIds = _availableIds.Count;
                if (remainingIds <= _currentBlockSize * _prefetchThreshold && 
                    Interlocked.CompareExchange(ref _fetchingInProgress, 1, 0) == 0)
                {
                    // We successfully set the flag, now fetch asynchronously
                    _ = Task.Run(async () => 
                    {
                        try
                        {
                            await GetNextBlockAsyncInternal(cancellationToken);
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _fetchingInProgress, 0);
                        }
                    });
                }
                
                // Track metrics if telemetry is available
                _telemetry?.TrackMetric("AvailableIds", _availableIds.Count, _scope);
                
                return id;
            }

            // If queue is empty, fetch a block and wait for it
            return await GetIdWithBlockFetchAsync(cancellationToken);
        }

        /// <summary>
        /// Recovers IDs that were skipped or not used.
        /// </summary>
        /// <remarks>
        /// This operation helps reuse IDs that were allocated but never used,
        /// which can happen in certain failure scenarios.
        /// </remarks>
        /// <exception cref="NotImplementedException">
        /// This method is currently not implemented and will throw an exception if called.
        /// </exception>
        public void RecoverSkippedIds()
        {
            // This method is still a placeholder for future implementation
            throw new NotImplementedException("RecoverSkippedIds is not yet implemented");
        }
        
        /// <summary>
        /// Recovers IDs that were skipped or not used asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This operation helps reuse IDs that were allocated but never used,
        /// which can happen in certain failure scenarios.
        /// </remarks>
        /// <exception cref="NotImplementedException">
        /// This method is currently not implemented and will throw an exception if called.
        /// </exception>
        public Task RecoverSkippedIdsAsync(CancellationToken cancellationToken = default)
        {
            // This method is still a placeholder for future implementation
            throw new NotImplementedException("RecoverSkippedIdsAsync is not yet implemented");
        }
        
        private void AdjustBlockSize()
        {
            lock (_nextBlockLock)
            {
                // Avoid multiple adjustments in a short time period
                if (DateTime.UtcNow - _lastBlockSizeAdjustment <= _blockSizeAdjustmentInterval)
                {
                    return;
                }
                
                // Calculate IDs used per minute
                double idsPerMinute = _idsFetchedSinceLastAdjustment / 
                    _blockSizeAdjustmentInterval.TotalMinutes;
                
                // Target ~2 minutes worth of IDs per block, but stay within limits
                int targetBlockSize = (int)Math.Min(_maxBlockSize, 
                    Math.Max(_minBlockSize, idsPerMinute * 2));
                
                // Track before changing
                _telemetry?.TrackMetric("BlockSizeAdjustment", 
                    targetBlockSize - _currentBlockSize, 
                    $"From: {_currentBlockSize}, To: {targetBlockSize}, IdsPerMin: {idsPerMinute:F2}");
                
                _currentBlockSize = targetBlockSize;
                _lastBlockSizeAdjustment = DateTime.UtcNow;
                _idsFetchedSinceLastAdjustment = 0;
            }
        }

        private T GetIdWithBlockFetch()
        {
            lock (_nextBlockLock)
            {
                // Check if another thread already added IDs while we were waiting for the lock
                if (_availableIds.TryDequeue(out var id))
                {
                    return id;
                }

                // If we're already getting a block, wait for it to complete
                if (_gettingNextBlock && _activeBlockFunction != null)
                {
                    _activeBlockFunction.Wait();
                    if (_availableIds.TryDequeue(out var newId))
                    {
                        return newId;
                    }
                }

                // We need to fetch a block now and wait for it
                using (_telemetry?.StartOperation("FetchBlockSynchronously", _scope))
                {
                    FetchBlockSynchronously();
                }
                
                if (_availableIds.TryDequeue(out var finalId))
                {
                    return finalId;
                }

                throw new InvalidOperationException($"Unable to get next id for {_scope}");
            }
        }
        
        private async Task<T> GetIdWithBlockFetchAsync(CancellationToken cancellationToken = default)
        {
            using var locker = await new AsyncLock(_nextBlockLock).LockAsync(cancellationToken);
            
            // Check if another thread already added IDs while we were waiting for the lock
            if (_availableIds.TryDequeue(out var id))
            {
                return id;
            }

            // If we're already getting a block, wait for it to complete
            if (_gettingNextBlock && _activeBlockFunction != null)
            {
                await _activeBlockFunction;
                if (_availableIds.TryDequeue(out var newId))
                {
                    return newId;
                }
            }

            // We need to fetch a block now and wait for it
            using (_telemetry?.StartOperation("FetchBlockAsynchronously", _scope))
            {
                await FetchBlockAsynchronouslyInternal(cancellationToken);
            }
            
            if (_availableIds.TryDequeue(out var finalId))
            {
                return finalId;
            }

            throw new InvalidOperationException($"Unable to get next id for {_scope}");
        }

        private void FetchBlockSynchronously()
        {
            var ids = _blockFunction(_currentBlockSize);
            foreach (var id in ids)
            {
                _availableIds.Enqueue(id);
            }
            
            _telemetry?.TrackMetric("BlockFetchedSize", ids.Count, _scope);
        }
        
        private async Task FetchBlockAsynchronouslyInternal(CancellationToken cancellationToken = default)
        {
            var ids = await _asyncBlockFunction(_currentBlockSize);
            foreach (var id in ids)
            {
                _availableIds.Enqueue(id);
            }
            
            _telemetry?.TrackMetric("BlockFetchedSize", ids.Count, _scope);
        }

        private void GetNextBlockAsync()
        {
            if (_gettingNextBlock)
            {
                return; // Already fetching a block
            }

            lock (_nextBlockLock)
            {
                if (_gettingNextBlock)
                {
                    return; // Double-check after acquiring the lock
                }

                _gettingNextBlock = true;
                _activeBlockFunction = Task.Run(() =>
                {
                    var result = _blockFunction(_currentBlockSize);
                    foreach (var x in result)
                    {
                        _availableIds.Enqueue(x);
                    }
                    
                    _telemetry?.TrackMetric("BlockFetchedSize", result.Count, _scope);
                    _telemetry?.TrackMetric("AvailableIdsAfterFetch", _availableIds.Count, _scope);
                    
                }).ContinueWith((e) =>
                {
                    if (e.IsFaulted)
                    {
                        _gettingNextBlock = false;
                        _telemetry?.TrackException(e.Exception, $"GetNextBlockAsync_{_scope}");
                        throw new InvalidOperationException("Failed to get the next block of IDs.", e.Exception);
                    }
                    else
                    {
                        _gettingNextBlock = false;
                        _activeBlockFunction = null;
                    }
                });
            }
        }
        
        private async Task GetNextBlockAsyncInternal(CancellationToken cancellationToken = default)
        {
            if (_gettingNextBlock)
            {
                return; // Already fetching a block
            }

            using var locker = await new AsyncLock(_nextBlockLock).LockAsync(cancellationToken);
            
            if (_gettingNextBlock)
            {
                return; // Double-check after acquiring the lock
            }

            _gettingNextBlock = true;
            
            try
            {
                var result = await _asyncBlockFunction(_currentBlockSize);
                foreach (var x in result)
                {
                    _availableIds.Enqueue(x);
                }
                
                _telemetry?.TrackMetric("BlockFetchedSize", result.Count, _scope);
                _telemetry?.TrackMetric("AvailableIdsAfterFetch", _availableIds.Count, _scope);
            }
            catch (Exception ex)
            {
                _telemetry?.TrackException(ex, $"GetNextBlockAsyncInternal_{_scope}");
                throw new InvalidOperationException("Failed to get the next block of IDs.", ex);
            }
            finally
            {
                _gettingNextBlock = false;
                _activeBlockFunction = null;
            }
        }
    }
}
