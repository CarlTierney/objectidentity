
using Microsoft.Extensions.DependencyInjection.Extensions;
using ObjectIdentity.Tests;

/// <summary>
    /// Additional tests for the generic identity functionality
    /// </summary>
    [TestClass]
    public class GenericIdentityTests
    {
        private static string _dbConnString = "";
        
        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            _dbConnString = TestConfig.Configuration.GetConnectionString("testdb");

            
        }

        [TestMethod]
        public void TestIdentityScopeGenericBlockRetrieval()
        {
            var options = Options.Create(new ObjectIdentityOptions {
                ConnectionString = _dbConnString,
                TableSchema = "dbo",
                DefaultBlockSize = 20
            });
            var store = new SqlIdentityStore(options);
            
            // Test the generic GetIds method directly
            string sequenceName = $"{options.Value.IdentitySchema}.TestSequence_{Guid.NewGuid():N}";
            
            // Create sequence
            ExecuteSql($"CREATE SEQUENCE {sequenceName} AS INT START WITH 1000 INCREMENT BY 1");
            
            try
            {
                // Get IDs using the generic method
                var intIds = SqlIdentityListHelper.GetIds<int>(_dbConnString, sequenceName, 10);
                
                // Verify results
                Assert.AreEqual(10, intIds.Count);
                Assert.AreEqual(1000, intIds[0]);
                Assert.AreEqual(1009, intIds[9]);
                
                // Get another block
                var nextIds = SqlIdentityListHelper.GetIds<int>(_dbConnString, sequenceName, 5);
                
                // Verify results of the second block
                Assert.AreEqual(5, nextIds.Count);
                Assert.AreEqual(1010, nextIds[0]);
                Assert.AreEqual(1014, nextIds[4]);
            }
            finally
            {
                // Clean up
                ExecuteSql($"DROP SEQUENCE IF EXISTS {sequenceName}");
            }
        }
        
        [TestMethod]
        public void TestLegacyAndGenericMethods()
        {
            var options = Options.Create(new ObjectIdentityOptions {
                ConnectionString = _dbConnString,
                TableSchema = "dbo",
                DefaultBlockSize = 20
            });
            
            // Create test sequences
            string intSequence = $"{options.Value.IdentitySchema}.IntSequence_{Guid.NewGuid():N}";
            string longSequence = $"{options.Value.IdentitySchema}.LongSequence_{Guid.NewGuid():N}";
            
            ExecuteSql($"CREATE SEQUENCE {intSequence} AS INT START WITH 100 INCREMENT BY 1");
            ExecuteSql($"CREATE SEQUENCE {longSequence} AS BIGINT START WITH 200 INCREMENT BY 1");
            
            try
            {
                // Test legacy methods
                var intIds1 = SqlIdentityListHelper.GetIntIds(_dbConnString, intSequence, 5);
                var longIds1 = SqlIdentityListHelper.GetLongIds(_dbConnString, longSequence, 5);
                
                // Test generic methods
                var intIds2 = SqlIdentityListHelper.GetIds<int>(_dbConnString, intSequence, 5);
                var longIds2 = SqlIdentityListHelper.GetIds<long>(_dbConnString, longSequence, 5);
                
                // Verify results are as expected
                Assert.AreEqual(5, intIds1.Count);
                Assert.AreEqual(5, longIds1.Count);
                Assert.AreEqual(5, intIds2.Count);
                Assert.AreEqual(5, longIds2.Count);
                
                // Verify the starting values
                Assert.AreEqual(100, intIds1[0]);
                Assert.AreEqual(200, longIds1[0]);
                Assert.AreEqual(105, intIds2[0]);
                Assert.AreEqual(205, longIds2[0]);
                
                // Verify types
                Assert.IsInstanceOfType(intIds1[0], typeof(int));
                Assert.IsInstanceOfType(longIds1[0], typeof(long));
                Assert.IsInstanceOfType(intIds2[0], typeof(int));
                Assert.IsInstanceOfType(longIds2[0], typeof(long));
            }
            finally
            {
                // Clean up
                ExecuteSql($"DROP SEQUENCE IF EXISTS {intSequence}");
                ExecuteSql($"DROP SEQUENCE IF EXISTS {longSequence}");
            }
        }
        
        private void ExecuteSql(string sql)
        {
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(_dbConnString))
            {
                conn.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }