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
    /// <summary>
    /// SQL Server implementation of <see cref="IIdentityStore"/> that uses SQL Server sequences for ID generation.
    /// </summary>
    /// <remarks>
    /// This implementation provides high-performance, distributed, and reliable ID generation using SQL Server sequences.
    /// It includes features such as:
    /// <list type="bullet">
    /// <item><description>Circuit breaker pattern for handling transient errors</description></item>
    /// <item><description>Caching of initialized scopes for performance</description></item>
    /// <item><description>Automatic sequence creation with appropriate starting values</description></item>
    /// <item><description>Support for both synchronous and asynchronous operations</description></item>
    /// <item><description>Telemetry for monitoring and diagnostics</description></item>
    /// </list>
    /// </remarks>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlIdentityStore"/> class.
        /// </summary>
        /// <param name="options">Configuration options for the identity store.</param>
        /// <param name="telemetry">Optional telemetry provider for monitoring and performance tracking.</param>
        /// <param name="logger">Optional logger for diagnostic information.</param>
        public SqlIdentityStore(IOptions<ObjectIdentityOptions> options, 
            IObjectIdentityTelemetry telemetry = null, 
            ILogger<SqlIdentityStore> logger = null)
        {
            var opts = options.Value ?? throw new ArgumentNullException(nameof(options));
            _connectionString = opts.ConnectionString ?? throw new ArgumentException("Connection string cannot be null", nameof(options));
            _tableSchema = opts.TableSchema ?? "dbo";
            _identitySchema = opts.IdentitySchema ?? "Identity";
            _isObjectNamePlural = opts.IsObjectNamePlural;
            _idFactoryObjectOrTypeName = opts.IdFactoryObjectOrTypeName ?? "ObjectOrTypeName";
            _identityColName = opts.IdentityColName ?? "Id";
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

        /// <summary>
        /// Initializes the identity store by ensuring the required database schema exists.
        /// </summary>
        /// <remarks>
        /// This method creates the identity schema in the database if it doesn't already exist.
        /// It uses a lock to ensure thread safety and implements a double-check pattern to minimize locking.
        /// </remarks>
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

        /// <summary>
        /// Initializes the identity store asynchronously by ensuring the required database schema exists.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous initialization operation.</returns>
        /// <remarks>
        /// This method creates the identity schema in the database if it doesn't already exist.
        /// It uses an async lock to ensure thread safety in asynchronous operations.
        /// </remarks>
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

        /// <summary>
        /// Initializes a scope and returns a function to get ID blocks.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope to initialize.</param>
        /// <param name="startingId">Optional starting ID value for the scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <returns>A function that takes a block size and returns a list of sequential IDs.</returns>
        /// <remarks>
        /// If the scope is already initialized, this method returns the existing ID block function.
        /// Otherwise, it creates a new sequence in the database with the appropriate starting value.
        /// </remarks>
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

        /// <summary>
        /// Initializes a scope asynchronously and returns a function to get ID blocks asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope to initialize.</param>
        /// <param name="startingId">Optional starting ID value for the scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a function that takes a block size and returns a task yielding a list of sequential IDs.</returns>
        /// <remarks>
        /// If the scope is already initialized, this method returns the existing asynchronous ID block function.
        /// Otherwise, it creates a new sequence in the database with the appropriate starting value.
        /// </remarks>
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

        /// <summary>
        /// Creates a function that retrieves a block of IDs from a SQL sequence.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope to get IDs for.</param>
        /// <returns>A function that takes a block size and returns a list of sequential IDs.</returns>
        /// <remarks>
        /// This method is used internally to create the delegate function returned by <see cref="Initialize{T}"/>.
        /// </remarks>
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

        /// <summary>
        /// Creates a function that asynchronously retrieves a block of IDs from a SQL sequence.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope to get IDs for.</param>
        /// <returns>A function that takes a block size and returns a task yielding a list of sequential IDs.</returns>
        /// <remarks>
        /// This method is used internally to create the asynchronous delegate function returned by <see cref="InitializeAsync{T}"/>.
        /// </remarks>
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

        /// <summary>
        /// Checks if a specific scope has been initialized in the storage system.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <returns><c>true</c> if the scope has been initialized; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method first checks an in-memory cache of initialized scopes for performance.
        /// If the scope is not found in the cache, it queries the database to check if the corresponding sequence exists.
        /// </remarks>
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

        /// <summary>
        /// Checks asynchronously if a specific scope has been initialized in the storage system.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result is <c>true</c> if the scope has been initialized; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method first checks an in-memory cache of initialized scopes for performance.
        /// If the scope is not found in the cache, it queries the database to check if the corresponding sequence exists.
        /// </remarks>
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

        /// <summary>
        /// Gets the table name for a scope, applying pluralization if configured.
        /// </summary>
        /// <param name="scope">The scope name to convert to a table name.</param>
        /// <returns>The table name for the scope.</returns>
        /// <remarks>
        /// If <see cref="ObjectIdentityOptions.IsObjectNamePlural"/> is set to true,
        /// this method will pluralize the scope name using the Pluralize.NET library.
        /// </remarks>
        public virtual string GetTableName(string scope)
        {
            var tableName = scope;
            if (_isObjectNamePlural)
                tableName = _pluralizer.Pluralize(tableName);

            return tableName;
        }

        /// <summary>
        /// Gets the fully-qualified sequence name for a scope.
        /// </summary>
        /// <param name="scope">The scope name to get the sequence name for.</param>
        /// <returns>The fully-qualified sequence name (schema.sequence).</returns>
        public virtual string GetSequenceName(string scope)
        {
            var tableName = GetTableName(scope);
            return $"{_identitySchema}.{tableName}";
        }

        /// <summary>
        /// Gets the recommended starting ID for a new scope based on existing data.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <returns>The recommended starting ID value, typically based on existing data with a safety buffer.</returns>
        /// <remarks>
        /// This method delegates to <see cref="GetInitialStartValueForSequence"/> to determine the appropriate starting value.
        /// </remarks>
        public virtual long GetInitialStartValueForScope(string scope) => GetInitialStartValueForSequence(scope);

        /// <summary>
        /// Gets the recommended starting ID for a new scope based on existing data asynchronously.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the recommended starting ID value.</returns>
        /// <remarks>
        /// This method delegates to <see cref="GetInitialStartValueForSequenceAsync"/> to determine the appropriate starting value.
        /// </remarks>
        public virtual async Task<long> GetInitialStartValueForScopeAsync(string scope, CancellationToken cancellationToken = default)
            => await GetInitialStartValueForSequenceAsync(scope, cancellationToken);

        /// <summary>
        /// Determines the initial starting value for a sequence based on existing data in the database.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <returns>The initial starting value for the sequence, with a safety buffer added.</returns>
        /// <remarks>
        /// This method checks both application tables and the IdFactory table to determine the highest
        /// existing ID for this scope, and adds a safety buffer to ensure no collisions.
        /// </remarks>
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

        /// <summary>
        /// Asynchronously determines the initial starting value for a sequence based on existing data in the database.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the initial starting value for the sequence, with a safety buffer added.</returns>
        /// <remarks>
        /// This method checks both application tables and the IdFactory table to determine the highest
        /// existing ID for this scope, and adds a safety buffer to ensure no collisions.
        /// </remarks>
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

        /// <summary>
        /// Retrieves the maximum ID value from the IdFactory table for a specific scope.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <returns>The maximum ID value found in the IdFactory table, or null if no value is found or the table doesn't exist.</returns>
        /// <remarks>
        /// This method is used by <see cref="GetInitialStartValueForSequence"/> to determine an appropriate starting value.
        /// </remarks>
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

        /// <summary>
        /// Asynchronously retrieves the maximum ID value from the IdFactory table for a specific scope.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the maximum ID value found in the IdFactory table, or null if no value is found or the table doesn't exist.</returns>
        /// <remarks>
        /// This method is used by <see cref="GetInitialStartValueForSequenceAsync"/> to determine an appropriate starting value.
        /// </remarks>
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

        /// <summary>
        /// Retrieves the maximum ID value from the application table associated with a specific scope.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <returns>The maximum ID value found in the application table, or null if no value is found or the table doesn't exist.</returns>
        /// <remarks>
        /// This method is used by <see cref="GetInitialStartValueForSequence"/> to determine an appropriate starting value.
        /// </remarks>
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

        /// <summary>
        /// Asynchronously retrieves the maximum ID value from the application table associated with a specific scope.
        /// </summary>
        /// <param name="scope">The name of the scope to check.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the maximum ID value found in the application table, or null if no value is found or the table doesn't exist.</returns>
        /// <remarks>
        /// This method is used by <see cref="GetInitialStartValueForSequenceAsync"/> to determine an appropriate starting value.
        /// </remarks>
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

        /// <summary>
        /// Creates a SQL sequence for a scope if it doesn't already exist.
        /// </summary>
        /// <param name="scope">The name of the scope to create a sequence for.</param>
        /// <param name="startValue">The starting value for the sequence.</param>
        /// <param name="maxValue">Optional maximum value for the sequence. If specified, the sequence will cycle back to the start after reaching this value.</param>
        /// <remarks>
        /// This method is called during initialization of a scope to ensure the necessary SQL sequence exists.
        /// The sequence is created with a cache size of 100 for performance.
        /// </remarks>
        public virtual void CreateSequenceIfMissingFor(string scope, long startValue, long? maxValue)
        {
            var tableName = GetTableName(scope);
            string sql = maxValue.HasValue
                ? $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100 MAXVALUE {maxValue.Value} CYCLE"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100";

            ExecuteNonQuery(sql);
            _initializedScopes[scope] = true;
        }

        /// <summary>
        /// Asynchronously creates a SQL sequence for a scope if it doesn't already exist.
        /// </summary>
        /// <param name="scope">The name of the scope to create a sequence for.</param>
        /// <param name="startValue">The starting value for the sequence.</param>
        /// <param name="maxValue">Optional maximum value for the sequence. If specified, the sequence will cycle back to the start after reaching this value.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method is called during asynchronous initialization of a scope to ensure the necessary SQL sequence exists.
        /// The sequence is created with a cache size of 100 for performance.
        /// </remarks>
        public virtual async Task CreateSequenceIfMissingForAsync(string scope, long startValue, long? maxValue, CancellationToken cancellationToken = default)
        {
            var tableName = GetTableName(scope);
            string sql = maxValue.HasValue
                ? $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100 MAXVALUE {maxValue.Value} CYCLE"
                : $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100";

            await ExecuteNonQueryAsync(sql, cancellationToken);
            _initializedScopes[scope] = true;
        }

        /// <summary>
        /// Gets the next block of sequential IDs for the specified scope.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope to get IDs for.</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <param name="startingId">Optional starting ID value for a new scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <returns>A list of sequential ID values of type <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// This method ensures the scope is initialized before retrieving the ID block.
        /// </remarks>
        public List<T> GetNextIdBlock<T>(string scope, int blockSize, long? startingId = null, long? maxValue = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            using var operation = _telemetry?.StartOperation("GetNextIdBlock", scope);
            
            // Ensure scope is initialized
            Initialize<T>(scope, startingId, maxValue);
            var blockFunc = IdBlockFunction<T>(scope);
            return blockFunc(blockSize);
        }

        /// <summary>
        /// Gets the next block of sequential IDs for the specified scope asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope to get IDs for.</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <param name="startingId">Optional starting ID value for a new scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of sequential ID values of type <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// This method ensures the scope is initialized before retrieving the ID block.
        /// </remarks>
        public async Task<List<T>> GetNextIdBlockAsync<T>(string scope, int blockSize, long? startingId = null, long? maxValue = null, CancellationToken cancellationToken = default)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            using var operation = _telemetry?.StartOperation("GetNextIdBlockAsync", scope);
            
            // Ensure scope is initialized
            var blockFunc = await InitializeAsync<T>(scope, startingId, maxValue, cancellationToken);
            return await blockFunc(blockSize);
        }
    }

    /// <summary>
    /// Provides an asynchronous locking mechanism for use in asynchronous methods.
    /// </summary>
    /// <remarks>
    /// This class combines a <see cref="SemaphoreSlim"/> for asynchronous waiting with a 
    /// standard lock for complete thread safety.
    /// </remarks>
    internal class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly object _syncLock;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLock"/> class.
        /// </summary>
        /// <param name="syncLock">The synchronization object to use for the lock.</param>
        public AsyncLock(object syncLock)
        {
            _semaphore = new SemaphoreSlim(1, 1);
            _syncLock = syncLock;
        }
        
        /// <summary>
        /// Asynchronously acquires the lock.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the lock acquisition.</param>
        /// <returns>A task representing the asynchronous operation. The task result is a disposable object that releases the lock when disposed.</returns>
        public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new ReleaseHandle(_semaphore, _syncLock);
        }
        
        /// <summary>
        /// A disposable object that releases the lock when disposed.
        /// </summary>
        private class ReleaseHandle : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private readonly object _syncLock;
            
            /// <summary>
            /// Initializes a new instance of the <see cref="ReleaseHandle"/> class.
            /// </summary>
            /// <param name="semaphore">The semaphore to release.</param>
            /// <param name="syncLock">The synchronization object to exit.</param>
            public ReleaseHandle(SemaphoreSlim semaphore, object syncLock)
            {
                _semaphore = semaphore;
                _syncLock = syncLock;
                Monitor.Enter(_syncLock);
            }
            
            /// <summary>
            /// Releases the lock.
            /// </summary>
            public void Dispose()
            {
                Monitor.Exit(_syncLock);
                _semaphore.Release();
            }
        }
    }
}
