using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    public static class SqlIdentityListHelper
    {
        /// <summary>
        /// Gets a block of IDs from a SQL sequence with generic type support
        /// </summary>
        public static List<T> GetIds<T>(string connectionString, string sequenceName, int blockSize) 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return GetIdsAsync<T>(connectionString, sequenceName, blockSize).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets a block of IDs from a SQL sequence with generic type support asynchronously
        /// </summary>
        public static async Task<List<T>> GetIdsAsync<T>(string connectionString, string sequenceName, int blockSize, CancellationToken cancellationToken = default)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(cancellationToken);
                
                using var cmd = new SqlCommand("sys.sp_sequence_get_range", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@sequence_name", sequenceName);
                cmd.Parameters.AddWithValue("@range_size", blockSize);
                cmd.Parameters.Add("@range_first_value", SqlDbType.Variant);
                cmd.Parameters["@range_first_value"].Direction = ParameterDirection.Output;
                cmd.Parameters.Add("@range_last_value", SqlDbType.Variant);
                cmd.Parameters["@range_last_value"].Direction = ParameterDirection.Output;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                
                // Retrieve the range values
                dynamic start = cmd.Parameters["@range_first_value"].Value;
                dynamic end = cmd.Parameters["@range_last_value"].Value;
                
                // Create a list of the appropriate type
                var ids = new List<T>();
                
                // Add values to the list with type conversion
                for (dynamic i = start; i <= end; i++)
                {
                    ids.Add((T)Convert.ChangeType(i, typeof(T)));
                }

                return ids;
            }
        }

        // Legacy methods that defer to the new generic method
        public static List<long> GetLongIds(string connectionString, string sequenceName, int blockSize)
        {
            return GetIds<long>(connectionString, sequenceName, blockSize);
        }

        public static List<int> GetIntIds(string connectionString, string sequenceName, int blockSize)
        {
            return GetIds<int>(connectionString, sequenceName, blockSize);
        }
        
        // Async versions of legacy methods
        public static Task<List<long>> GetLongIdsAsync(string connectionString, string sequenceName, int blockSize, CancellationToken cancellationToken = default)
        {
            return GetIdsAsync<long>(connectionString, sequenceName, blockSize, cancellationToken);
        }

        public static Task<List<int>> GetIntIdsAsync(string connectionString, string sequenceName, int blockSize, CancellationToken cancellationToken = default)
        {
            return GetIdsAsync<int>(connectionString, sequenceName, blockSize, cancellationToken);
        }
    }
}
