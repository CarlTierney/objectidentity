using Microsoft.Data.SqlClient;
using Pluralize.NET;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ObjectIdentity
{
    public class SqlIdentityScopeInitializer : IIdentityScopeInitializer
    {
        private readonly string _connectionString;
        private readonly string _tableSchema;
        private readonly string _identitySchema;
        private readonly bool _isObjectNamePlural;
        private readonly object _lock = new object();
        private readonly string _identityColName;
        private IPluralize _pluralizer = new Pluralizer();
        private long _initialSafetyBuffer = 1000;
        private bool _dbInitialized = false;
        private ConcurrentDictionary<string, bool> _initializedScopes = new ConcurrentDictionary<string, bool>();
        private string _idFactoryObjectOrTypeName;

        public SqlIdentityScopeInitializer(IOptions<ObjectIdentityOptions> options)
        {
            var opts = options.Value;
            _connectionString = opts.ConnectionString;
            _tableSchema = opts.TableSchema;
            _identitySchema = opts.IdentitySchema;
            _isObjectNamePlural = opts.IsObjectNamePlural;
            _idFactoryObjectOrTypeName = opts.IdFactoryObjectOrTypeName;
            _identityColName = opts.IdentityColName;
            _dbInitialized = false;
            Initialize();
        }

        public virtual void Initialize()
        {
            if (_dbInitialized) return;
            lock (_lock)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand($"IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = '{_identitySchema}') EXEC('create schema {_identitySchema}')", conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (SqlException e)
                {
                    throw new InvalidOperationException("Failed to initialize the database schema.", e);
                }

                _dbInitialized = true;
            }
        }

        public virtual Func<int, List<T>> Initialize<T>(string scope, long? startingId = null, long? maxValue = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            if (IsInitialized(scope))
                return IdBlockFunction<T>(scope);

            lock (_lock)
            {
                var start = startingId ?? GetInitialStartValueForSequence(scope);
                CreateSequenceIfMissingFor(scope, start, maxValue);

                return IdBlockFunction<T>(scope);
            }
        }

        protected Func<int, List<T>> IdBlockFunction<T>(string scope) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            if (typeof(T) == typeof(int))
            {
                return blocksize =>
                {
                    var sequencename = GetSequenceName(scope);
                    var results = SqlIdentityListHelper.GetIntIds(_connectionString, sequencename, blocksize);
                    return results as List<T>;
                };
            }

            if (typeof(T) == typeof(long))
            {
                return blocksize =>
                {
                    var sequencename = GetSequenceName(scope);
                    var results = SqlIdentityListHelper.GetLongIds(_connectionString, sequencename, blocksize);
                    return results as List<T>;
                };
            }

            throw new NotSupportedException($"Identity type of {typeof(T).Name} is not supported for SQL identity.");
        }

        protected virtual bool IsInitialized(string scope)
        {
            if (_initializedScopes.ContainsKey(scope))
                return true;

            lock (_lock)
            {
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

        public virtual long GetInitialStartValueForSequence(string scope)
        {
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

        protected virtual long? GetMaxValueFromIdFactory(string scope)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmd = new SqlCommand($"SELECT LastID FROM {_tableSchema}.IdFactory WHERE {_idFactoryObjectOrTypeName} = '{scope}'", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            return reader.GetInt64(0);
                        }
                    }
                }
            }
            catch (SqlException e)
            {
                if (!e.Message.Contains("Invalid object name"))
                {
                    throw new InvalidOperationException("Failed to retrieve max value from IdFactory.", e);
                }
            }

            return null;
        }

        protected virtual long? GetMaxValueFromTableByScopeName(string scope)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmd = new SqlCommand($"SELECT MAX({_identityColName}) FROM {_tableSchema}.{GetTableName(scope)}", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            return reader.GetInt64(0);
                        }
                    }
                }
            }
            catch (SqlException e)
            {
                if (!e.Message.Contains("Invalid object name") && !e.Message.Contains("Invalid column name"))
                {
                    throw new InvalidOperationException("Failed to retrieve max value from table by scope name.", e);
                }
            }

            return null;
        }

        public virtual void CreateSequenceIfMissingFor(string scope, long startValue, long? maxValue)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var tableName = GetTableName(scope);

                string sql = maxValue.HasValue
                    ? $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100 MAXVALUE {maxValue.Value} CYCLE"
                    : $"IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{tableName}' AND schema_id = SCHEMA_ID('{_identitySchema}')) CREATE SEQUENCE {_identitySchema}.{tableName} AS BIGINT START WITH {startValue} INCREMENT BY 1 CACHE 100";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                _initializedScopes[scope] = true;
            }
        }
    }
}
