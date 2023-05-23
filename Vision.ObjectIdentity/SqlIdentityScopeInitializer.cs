using Microsoft.Data.SqlClient;
using Pluralize.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vision.ObjectIdentity
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
        private List<string> _initializedScopes = new List<string>();


        public SqlIdentityScopeInitializer(string connectionString, 
            string tableSchema, 
            int cacheSize,
            bool isObjectNamePlural = true,
            string identityColName = "Id", 
            string identitySchema = "ids")
        {
            _isObjectNamePlural = isObjectNamePlural;
            _tableSchema = tableSchema;
            _identitySchema = identitySchema;
            _connectionString = connectionString;
            _identityColName = identityColName;
            _dbInitialized = false;

            Initialize();
        }


        public virtual void Initialize()
        {
            if(_dbInitialized) return;
            lock(_lock)
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand($"IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = '{_identitySchema}') EXEC('create schema {_identitySchema}')", conn);
                    cmd.ExecuteNonQuery();

                    conn.Close();

                }
                _dbInitialized=true;
            }
           
        }

        public virtual Func<int,List<T>> Initialize<T>(string scope, int? startingId = null) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            if (IsInitialized(scope))
                return IdBlockFunction<T>(scope);

            lock (_lock)
            {
                var start = (startingId.HasValue) ? startingId.Value : GetInitialStartValueForSequence(scope);
                CreateSequenceIfMissingFor(scope,start);

                return IdBlockFunction<T>(scope);
            }
        }


        protected  Func<int,List<T>> IdBlockFunction<T>(string scope) where T : struct, IComparable , IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            if(typeof(T) == typeof(int))
            {
                return new Func<int, List<T>>((blocksize) =>
                {

                    var sequencename = GetSequenceName(scope);
                    var results = SqlIdentityListHelper.GetIntIds(_connectionString, sequencename, blocksize);
                    return results as List<T>;

                });
            }

            if (typeof(T) == typeof(long))
            {
                return new Func<int, List<T>>((blocksize) =>
                {

                    var sequencename = GetSequenceName(scope);
                    var results = SqlIdentityListHelper.GetLongIds(_connectionString, sequencename, blocksize);
                    return results as List<T>;

                });
            }

            //TODO: add support for guid
            //if(typeof(T) == typeof(Guid))
            //{
            //    return new Func<int, List<T>>((blocksize) =>
            //    {

            //        var sequencename = GetSequenceName(scope);
            //        var results = SqlIdentityListHelper.GetGuidIds(_connectionString, sequencename, blocksize);
            //        return results as List<T>;

            //    });
            //}


            throw new NotSupportedException($"identity type of {typeof(T).Name} is not supported for sql identity");
        }


        protected virtual bool IsInitialized(string scope)
        {
            if (_initializedScopes.Any(a=> a== scope))
                return true;

            lock(_lock)
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var tableName = GetTableName(scope);

                    var cmd = new SqlCommand($"select 1 from sys.sequences where name = '{tableName}' and schema_id = SCHEMA_ID('{_identitySchema}')", conn);

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var found = reader.GetInt32(0);
                        if(found == 1)
                        {
                            _initializedScopes.Add(scope);
                            return true;
                        }
                            
                    }

                    conn.Close();
                    
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

                var cmd = new SqlCommand($"select max({_identityColName}) " +
                    $"from {_tableSchema}.{GetTableName(scope)}", conn);

                var reader = cmd.ExecuteReader();
                while(reader.Read())
                {
                    if (reader.IsDBNull(0))
                        startValue = 1;
                    else 
                        startValue = reader.GetInt64(0);
                }


                conn.Close();

                return startValue + _initialSafetyBuffer;
            }
        }

        public virtual void CreateSequenceIfMissingFor(string scope, long startValue)
        {

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var tableName = GetTableName(scope);
                
                
                var cmd = new SqlCommand($"if not exists (select 1 from sys.sequences where name = '{tableName}' and schema_id = SCHEMA_ID('{_identitySchema}'))"+
                            $" create sequence {_identitySchema}.{tableName} as bigint start with {startValue} increment by 1 cache {100}", conn);

                cmd.ExecuteNonQuery();

                conn.Close();
                _initializedScopes.Add(scope);
            }
        }


       


    }

       

    
}
