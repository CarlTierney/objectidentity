using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    public class IdentityScope<T> : IIdentityScope<T> where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        private readonly Type _idType;
        private int _currentBlockSize;
        private readonly ConcurrentQueue<T> _availableIds;
        private readonly string _scope;
        private readonly Func<int, List<T>> _blockFunction;
        private Func<int, Task<List<T>>> _asyncBlockFunction;
        private int _fetchingInProgress;
        private bool _gettingNextBlock;
        private Task _activeBlockFunction;
        private readonly object _nextBlockLock = new object();
        private readonly double _prefetchThreshold = 0.2; // Fetch new block when queue is at 20% capacity
        
        // Adaptive block size properties
        private readonly int _minBlockSize;
        private readonly int _maxBlockSize;
        private readonly TimeSpan _blockSizeAdjustmentInterval = TimeSpan.FromMinutes(5);
        private DateTime _lastBlockSizeAdjustment = DateTime.UtcNow;
        private int _idsFetchedSinceLastAdjustment = 0;
        private readonly IObjectIdentityTelemetry _telemetry;

        public IdentityScope(
            int blockSize,
            string scope,
            Func<int, List<T>> blockFunction,
            IObjectIdentityTelemetry telemetry = null
            )
        {
            _idType = typeof(T);
            _scope = scope;
            _currentBlockSize = blockSize;
            _blockFunction = blockFunction;
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
        
        public IdentityScope(
            int blockSize,
            string scope,
            Func<int, Task<List<T>>> asyncBlockFunction,
            IObjectIdentityTelemetry telemetry = null
            )
        {
            _idType = typeof(T);
            _scope = scope;
            _currentBlockSize = blockSize;
            _asyncBlockFunction = asyncBlockFunction;
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

        public Type IdType => _idType;

        public string Scope => _scope;
        
        // Implement the interface properties for monitoring
        public int CurrentBlockSize => _currentBlockSize;
        
        public int AvailableIdsCount => _availableIds.Count;

        public void CacheNextBlock()
        {
            // Start fetching a new block if needed
            if (_availableIds.Count <= _currentBlockSize * _prefetchThreshold)
            {
                GetNextBlockAsync();
            }
        }
        
        public async Task CacheNextBlockAsync(CancellationToken cancellationToken = default)
        {
            // Start fetching a new block if needed
            if (_availableIds.Count <= _currentBlockSize * _prefetchThreshold)
            {
                await GetNextBlockAsyncInternal(cancellationToken);
            }
        }

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

        public void RecoverSkippedIds()
        {
            // This method is still a placeholder for future implementation
            throw new NotImplementedException("RecoverSkippedIds is not yet implemented");
        }
        
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
