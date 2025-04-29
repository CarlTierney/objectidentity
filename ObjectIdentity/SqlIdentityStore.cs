using Microsoft.Data.SqlClient;
using Pluralize.NET;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Microsoft.Extensions.Logging;

namespace ObjectIdentity
{
    public class SqlIdentityStore : IIdentityStore
    {
        private readonly string _connectionString;
        private readonly string _tableSchema;
        private readonly string _identitySchema;
        private readonly bool _isObjectNamePlural;
        private readonly object _lock = new object();
        private readonly string _identityColName;
        private readonly IPluralize _pluralizer = new Pluralizer();
        private readonly long _initialSafetyBuffer = 1000;
        private readonly IObjectIdentityTelemetry _telemetry;
        private readonly ILogger<SqlIdentityStore> _logger;
        private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
        
        private bool _dbInitialized = false;
        private readonly ConcurrentDictionary<string, bool> _initializedScopes = new ConcurrentDictionary<string, bool>();
        private readonly string _idFactoryObjectOrTypeName;

        public SqlIdentityStore(IOptions<ObjectIdentityOptions> options, 
            IObjectIdentityTelemetry telemetry = null, 
            ILogger<SqlIdentityStore> logger = null)
        {
            var opts = options.Value;
            _connectionString = opts.ConnectionString;
            _tableSchema = opts.TableSchema;
            _identitySchema = opts.IdentitySchema;
            _isObjectNamePlural = opts.IsObjectNamePlural;
            _idFactoryObjectOrTypeName = opts.IdFactoryObjectOrTypeName;
            _identityColName = opts.IdentityColName;
            _telemetry = telemetry;
            _logger = logger;
            
            // Initialize circuit breaker for SQL operations
            _circuitBreaker = Policy
                .Handle<SqlException>(ex => IsTransientError(ex))
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, breakDelay) => 
                    {
                        _logger?.LogWarning(ex, "Circuit breaker opened for SQL operations. Retry after {BreakDelay}s", breakDelay.TotalSeconds);
                        _telemetry?.TrackException(ex, "CircuitBreakerOpened");
                    },
                    onReset: () => 
                    {
                        _logger?.LogInformation("Circuit breaker reset for SQL operations");
                    },
                    onHalfOpen: () => 
                    {
                        _logger?.LogInformation("Circuit breaker half-open for SQL operations");
                    }
                );
            
            _dbInitialized = false;
            Initialize();
        }

        // Check if SQL exception is transient
        private bool IsTransientError(SqlException ex)
        {
            int[] transientErrorNumbers = { 4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001 };
            return transientErrorNumbers.Contains(ex.Number);
        }

        // Helper method for connection management with async support
        private async Task<T> ExecuteWithConnectionAsync<T>(Func<SqlConnection, Task<T>> action, CancellationToken cancellationToken = default)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            return await action(conn);
        }

        // Helper method for connection management (sync version)
        private T ExecuteWithConnection<T>(Func<SqlConnection, T> action)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            return action(conn);
        }

        // Execute SQL command without return value (async)
        private async Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default)
        {
            await _circuitBreaker.ExecuteAsync(async () =>
            {
                using var scope = _telemetry?.StartOperation("SqlNonQuery", sql.Substring(0, Math.Min(50, sql.Length)));
                
                await ExecuteWithConnectionAsync(async conn =>
                {
                    using var cmd = new SqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    return true;
                }, cancellationToken);
            });
        }

        // Execute SQL command without return value (sync)
        private void ExecuteNonQuery(string sql)
        {
            using var scope = _telemetry?.StartOperation("SqlNonQuery", sql.Substring(0, Math.Min(50, sql.Length)));
            
            ExecuteWithConnection(conn =>
            {
                using var cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                return true;
            });
        }

        public virtual void Initialize()
        {
            if (_dbInitialized) return;
            lock (_lock)
            {
                if (_dbInitialized) return; // Double-check after lock

                try
                {
                    ExecuteNonQuery($"IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = '{_identitySchema}') EXEC('create schema {_identitySchema}')");
                    _dbInitialized = true;
                }
                catch (SqlException e)
                {
                    _telemetry?.TrackException(e, "Initialize");
                    throw new InvalidOperationException("Failed to initialize the database schema.", e);
                }
            }
        }

        public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_dbInitialized) return;
            
            // Use AsyncLock pattern instead of C# lock for async safety
            using (await new AsyncLock(_lock).LockAsync(cancellationToken))
            {
                if (_dbInitialized) return; // Double-check after lock

                try
                {
                    await ExecuteNonQueryAsync($"IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = '{_identitySchema}') EXEC('create schema {_identitySchema}')", cancellationToken);
                    _dbInitialized = true;
                }
                catch (SqlException e)
                {
                    _telemetry?.TrackException(e, "InitializeAsync");
                    throw new InvalidOperationException("Failed to initialize the database schema.", e);
                }
            }
        }

        public virtual Func<int, List<T>> Initialize<T>(string scope, long? startingId = null, long? maxValue = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            using var operation = _telemetry?.StartOperation("Initialize", scope);
            
            if (IsInitialized(scope))
                return IdBlockFunction<T>(scope);

            lock (_lock)
            {
                var start = startingId ?? GetInitialStartValueForSequence(scope);
                CreateSequenceIfMissingFor(scope, start, maxValue);

                return IdBlockFunction<T>(scope);
            }
        }

        public virtual async Task<Func<int, Task<List<T>>>> InitializeAsync<T>(string scope, long? startingId = null, long? maxValue = null, CancellationToken cancellationToken = default)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            using var operation = _telemetry?.StartOperation("InitializeAsync", scope);
            
            if (await IsInitializedAsync(scope, cancellationToken))
                return IdBlockFunctionAsync<T>(scope);

            using (await new AsyncLock(_lock).LockAsync(cancellationToken))
            {
                var start = startingId ?? await GetInitialStartValueForSequenceAsync(scope, cancellationToken);
                await CreateSequenceIfMissingForAsync(scope, start, maxValue, cancellationToken);

                return IdBlockFunctionAsync<T>(scope);
            }
        }

        protected Func<int, List<T>> IdBlockFunction<T>(string scope) 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var sequenceName = GetSequenceName(scope);
            
            return blockSize => 
            {
                using var operation = _telemetry?.StartOperation("GetIds", scope);
                _telemetry?.TrackMetric("RequestedBlockSize", blockSize, scope);
                
                return SqlIdentityListHelper.GetIds<T>(_connectionString, sequenceName, blockSize);
            };
        }

        protected Func<int, Task<List<T>>> IdBlockFunctionAsync<T>(string scope) 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var sequenceName = GetSequenceName(scope);
            
            return async (blockSize) => 
            {
                using var operation = _telemetry?.StartOperation("GetIdsAsync", scope);
                _telemetry?.TrackMetric("RequestedBlockSize", blockSize, scope);
                
                return await _circuitBreaker.ExecuteAsync(async () => 
                    await SqlIdentityListHelper.GetIdsAsync<T>(_connectionString, sequenceName, blockSize));
            };
        }

        public virtual bool IsInitialized(string scope)
        {
            if (_initializedScopes.TryGetValue(scope, out var isInitialized) && isInitialized)
                return true;

            lock (_lock)
            {
                using var operation = _telemetry?.StartOperation("IsInitialized", scope);
                
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var tableName = GetTableName(scope);

                    using (var cmd = new SqlCommand($"SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && reader.GetInt32(0) == 1)
                        {
                            _initializedScopes[scope] = true;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public virtual async Task<bool> IsInitializedAsync(string scope, CancellationToken cancellationToken = default)
        {
            if (_initializedScopes.TryGetValue(scope, out var isInitialized) && isInitialized)
                return true;

            using var operation = _telemetry?.StartOperation("IsInitializedAsync", scope);
            
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                using (await new AsyncLock(_lock).LockAsync(cancellationToken))
                {
                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync(cancellationToken);
                    
                    var tableName = GetTableName(scope);
                    using var cmd = new SqlCommand($"SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')", conn);
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    
                    if (await reader.ReadAsync(cancellationToken) && reader.GetInt32(0) == 1)
                    {
                        _initializedScopes[scope] = true;
                        return true;
                    }
                }
                return false;
            });
        }

        public virtual string GetTableName(string scope)
        {
            var tableName = scope;
            if (_isObjectNamePlural)
                tableName = _pluralizer.Pluralize(tableName);

            return tableName;
        }

        public virtual string GetSequenceName(string scope)
        {
            var tableName = GetTableName(scope);
            return $"{_identitySchema}.{tableName}";
        }

        public virtual long GetInitialStartValueForScope(string scope) => GetInitialStartValueForSequence(scope);

        public virtual async Task<long> GetInitialStartValueForScopeAsync(string scope, CancellationToken cancellationToken = default)
            => await GetInitialStartValueForSequenceAsync(scope, cancellationToken);

        public virtual long GetInitialStartValueForSequence(string scope)
        {
            using var operation = _telemetry?.StartOperation("GetInitialStartValue", scope);
            
            long startValue = 1;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var maxValueFound = GetMaxValueFromTableByScopeName(scope);
                if (maxValueFound.HasValue)
                {
                    startValue = maxValueFound.Value;
                    return startValue + _initialSafetyBuffer;
                }
                else
                {
                    maxValueFound = GetMaxValueFromIdFactory(scope);
                    if (maxValueFound.HasValue)
                    {
                        startValue = maxValueFound.Value;
                        return startValue + _initialSafetyBuffer;
                    }
                }
            }

            return startValue;
        }

        public virtual async Task<long> GetInitialStartValueForSequenceAsync(string scope, CancellationToken cancellationToken = default)
        {
            using var operation = _telemetry?.StartOperation("GetInitialStartValueAsync", scope);
            
            long startValue = 1;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync(cancellationToken);
                var maxValueFound = await GetMaxValueFromTableByScopeNameAsync(scope, cancellationToken);
                if (maxValueFound.HasValue)
                {
                    startValue = maxValueFound.Value;
                    return startValue + _initialSafetyBuffer;
                }
                else
                {
                    maxValueFound = await GetMaxValueFromIdFactoryAsync(scope, cancellationToken);
                    if (maxValueFound.HasValue)
                    {
                        startValue = maxValueFound.Value;
                        return startValue + _initialSafetyBuffer;
                    }
                }
            }

            return startValue;
        }

        protected virtual long? GetMaxValueFromIdFactory(string scope)
        {
            try
            {
                return ExecuteWithConnection<long?>(conn =>
                {
                    using (var cmd = new SqlCommand($"SELECT LastID FROM {_tableSchema}.IdFactory WHERE {_idFactoryObjectOrTypeName} = '{scope}'", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            return reader.GetInt64(0);
                        }
                    }
                    return (long?)null;
                });
            }
            catch (SqlException e)
            {
                if (!e.Message.Contains("Invalid object name"))
                {
                    _telemetry?.TrackException(e, $"GetMaxValueFromIdFactory_{scope}");
                    throw new InvalidOperationException("Failed to retrieve max value from IdFactory.", e);
                }
                return null;
            }
        }

        protected virtual async Task<long?> GetMaxValueFromIdFactoryAsync(string scope, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _circuitBreaker.ExecuteAsync(async () => 
                {
                    return await ExecuteWithConnectionAsync(async conn =>
                    {
                        using var cmd = new SqlCommand($"SELECT LastID FROM {_tableSchema}.IdFactory WHERE {_idFactoryObjectOrTypeName} = '{scope}'", conn);
                        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                        
                        if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0))
                        {
                            return reader.GetInt64(0);
                        }
                        return (long?)null;
                    }, cancellationToken);
                });
            }
            catch (SqlException e)
            {
                if (!e.Message.Contains("Invalid object name"))
                {
                    _telemetry?.TrackException(e, $"GetMaxValueFromIdFactoryAsync_{scope}");
                    throw new InvalidOperationException("Failed to retrieve max value from IdFactory.", e);
                }
                return null;
            }
        }

        protected virtual long? GetMaxValueFromTableByScopeName(string scope)
        {
            try
            {
                return ExecuteWithConnection<long?>(conn =>
                {
                    using (var cmd = new SqlCommand($"SELECT MAX({_identityColName}) FROM {_tableSchema}.{GetTableName(scope)}", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            return reader.GetInt64(0);
                        }
                    }
                    return (long?)null;
                });
            }
            catch (SqlException e)
            {
                if (!e.Message.Contains("Invalid object name") && !e.Message.Contains("Invalid column name"))
                {
                    _telemetry?.TrackException(e, $"GetMaxValueFromTableByScopeName_{scope}");
                    throw new InvalidOperationException("Failed to retrieve max value from table by scope name.", e);
                }
                return null;
            }
        }

        protected virtual async Task<long?> GetMaxValueFromTableByScopeNameAsync(string scope, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _circuitBreaker.ExecuteAsync(async () => 
                {
                    return await ExecuteWithConnectionAsync(async conn =>
                    {
                        using var cmd = new SqlCommand($"SELECT MAX({_identityColName}) FROM {_tableSchema}.{GetTableName(scope)}", conn);
                        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                        
                        if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0))
                        {
                            return reader.GetInt64(0);
                        }
                        return (long?)null;
                    }, cancellationToken);
                });
            }
            catch (SqlException e)
            {
                if (!e.Message.Contains("Invalid object name") && !e.Message.Contains("Invalid column name"))
                {
                    _telemetry?.TrackException(e, $"GetMaxValueFromTableByScopeNameAsync_{scope}");
                    throw new InvalidOperationException("Failed to retrieve max value from table by scope name.", e);
                }
                return null;
            }
        }

        public virtual void CreateSequenceIfMissingFor(string scope, long startValue, long? maxValue)
        {
            var tableName = GetTableName(scope);
            string sql = maxValue.HasValue
                ? $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100 MAXVALUE {maxValue.Value} CYCLE"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100";

            ExecuteNonQuery(sql);
            _initializedScopes[scope] = true;
        }

        public virtual async Task CreateSequenceIfMissingForAsync(string scope, long startValue, long? maxValue, CancellationToken cancellationToken = default)
        {
            var tableName = GetTableName(scope);
            string sql = maxValue.HasValue
                ? $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100 MAXVALUE {maxValue.Value} CYCLE"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100";

            await ExecuteNonQueryAsync(sql, cancellationToken);
            _initializedScopes[scope] = true;
        }

        public List<T> GetNextIdBlock<T>(string scope, int blockSize, long? startingId = null, long? maxValue = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            using var operation = _telemetry?.StartOperation("GetNextIdBlock", scope);
            
            // Ensure scope is initialized
            Initialize<T>(scope, startingId, maxValue);
            var blockFunc = IdBlockFunction<T>(scope);
            return blockFunc(blockSize);
        }

        public async Task<List<T>> GetNextIdBlockAsync<T>(string scope, int blockSize, long? startingId = null, long? maxValue = null, CancellationToken cancellationToken = default)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            using var operation = _telemetry?.StartOperation("GetNextIdBlockAsync", scope);
            
            // Ensure scope is initialized
            var blockFunc = await InitializeAsync<T>(scope, startingId, maxValue, cancellationToken);
            return await blockFunc(blockSize);
        }
    }

    // Helper class for async locking
    internal class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly object _syncLock;
        
        public AsyncLock(object syncLock)
        {
            _semaphore = new SemaphoreSlim(1, 1);
            _syncLock = syncLock;
        }
        
        public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new ReleaseHandle(_semaphore, _syncLock);
        }
        
        private class ReleaseHandle : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private readonly object _syncLock;
            
            public ReleaseHandle(SemaphoreSlim semaphore, object syncLock)
            {
                _semaphore = semaphore;
                _syncLock = syncLock;
                Monitor.Enter(_syncLock);
            }
            
            public void Dispose()
            {
                Monitor.Exit(_syncLock);
                _semaphore.Release();
            }
        }
    }
}
