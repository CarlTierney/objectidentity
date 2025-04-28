using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace ObjectIdentity
{
    public static class SqlIdentityListHelper
    {
       
       

        public static List<long> GetLongIds(string connectionString, string sequenceName, int blockSize)
        {
            using (var conn = new SqlConnection(connectionString))
            {


                conn.Open();
                var cmd = new SqlCommand("sys.sp_sequence_get_range", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@sequence_name", sequenceName);
                cmd.Parameters.AddWithValue("@range_size", blockSize);
                cmd.Parameters.Add("@range_first_value", SqlDbType.Variant);
                cmd.Parameters["@range_first_value"].Direction = ParameterDirection.Output;
                cmd.Parameters.Add("@range_last_value", SqlDbType.Variant);
                cmd.Parameters["@range_last_value"].Direction = ParameterDirection.Output;

                cmd.ExecuteNonQuery();


                var start = System.Convert.ToInt64(cmd.Parameters["@range_first_value"].Value);
                var end = System.Convert.ToInt64(cmd.Parameters["@range_last_value"].Value);
                




                conn.Close();

                var ids = new List<long>();
                for (var i = start; i <= end; i++)
                    ids.Add(i);

                return ids;

            }
        }


        public static List<int> GetIntIds(string connectionString, string sequenceName, int blockSize)
        {
            using (var conn = new SqlConnection(connectionString))
            {


                conn.Open();
                var cmd = new SqlCommand("sys.sp_sequence_get_range", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@sequence_name", sequenceName);
                cmd.Parameters.AddWithValue("@range_size", blockSize);
                cmd.Parameters.Add("@range_first_value", SqlDbType.Variant);
                cmd.Parameters["@range_first_value"].Direction = ParameterDirection.Output;
                cmd.Parameters.Add("@range_last_value", SqlDbType.Variant);
                cmd.Parameters["@range_last_value"].Direction = ParameterDirection.Output;

                cmd.ExecuteNonQuery();


                var start = System.Convert.ToInt32(cmd.Parameters["@range_first_value"].Value);
                var end = System.Convert.ToInt32(cmd.Parameters["@range_last_value"].Value);





                conn.Close();

                var ids = new List<int>();
                for (var i = start; i <= end; i++)
                    ids.Add(i);

                return ids;

            }
        }
    }

}
