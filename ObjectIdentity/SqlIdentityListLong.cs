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
    /// <summary>
    /// Helper class for retrieving blocks of sequential IDs from SQL Server sequences.
    /// </summary>
    /// <remarks>
    /// This class provides methods to retrieve ranges of IDs from SQL Server sequences,
    /// supporting both synchronous and asynchronous operations, and different numeric types 
    /// such as int and long.
    /// </remarks>
    public static class SqlIdentityListHelper
    {
        /// <summary>
        /// Gets a block of IDs from a SQL sequence with generic type support.
        /// </summary>
        /// <typeparam name="T">The type of IDs to retrieve.</typeparam>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="sequenceName">The fully-qualified name of the sequence (schema.sequence).</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <returns>A list of sequential IDs of type T.</returns>
        public static List<T> GetIds<T>(string connectionString, string sequenceName, int blockSize) 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return GetIdsAsync<T>(connectionString, sequenceName, blockSize).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets a block of IDs from a SQL sequence with generic type support asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of IDs to retrieve.</typeparam>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="sequenceName">The fully-qualified name of the sequence (schema.sequence).</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of sequential IDs of type T.</returns>
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

        /// <summary>
        /// Gets a block of long (Int64) IDs from a SQL sequence.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="sequenceName">The fully-qualified name of the sequence (schema.sequence).</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <returns>A list of sequential long IDs.</returns>
        /// <remarks>
        /// This is a legacy method that calls the generic <see cref="GetIds{T}"/> method with type long.
        /// </remarks>
        public static List<long> GetLongIds(string connectionString, string sequenceName, int blockSize)
        {
            return GetIds<long>(connectionString, sequenceName, blockSize);
        }

        /// <summary>
        /// Gets a block of integer (Int32) IDs from a SQL sequence.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="sequenceName">The fully-qualified name of the sequence (schema.sequence).</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <returns>A list of sequential integer IDs.</returns>
        /// <remarks>
        /// This is a legacy method that calls the generic <see cref="GetIds{T}"/> method with type int.
        /// </remarks>
        public static List<int> GetIntIds(string connectionString, string sequenceName, int blockSize)
        {
            return GetIds<int>(connectionString, sequenceName, blockSize);
        }
        
        /// <summary>
        /// Asynchronously gets a block of long (Int64) IDs from a SQL sequence.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="sequenceName">The fully-qualified name of the sequence (schema.sequence).</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of sequential long IDs.</returns>
        /// <remarks>
        /// This is a legacy method that calls the generic <see cref="GetIdsAsync{T}"/> method with type long.
        /// </remarks>
        public static Task<List<long>> GetLongIdsAsync(string connectionString, string sequenceName, int blockSize, CancellationToken cancellationToken = default)
        {
            return GetIdsAsync<long>(connectionString, sequenceName, blockSize, cancellationToken);
        }

        /// <summary>
        /// Asynchronously gets a block of integer (Int32) IDs from a SQL sequence.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="sequenceName">The fully-qualified name of the sequence (schema.sequence).</param>
        /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of sequential integer IDs.</returns>
        /// <remarks>
        /// This is a legacy method that calls the generic <see cref="GetIdsAsync{T}"/> method with type int.
        /// </remarks>
        public static Task<List<int>> GetIntIdsAsync(string connectionString, string sequenceName, int blockSize, CancellationToken cancellationToken = default)
        {
            return GetIdsAsync<int>(connectionString, sequenceName, blockSize, cancellationToken);
        }
    }
}
