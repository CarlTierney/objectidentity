using Microsoft.Extensions.DependencyInjection;
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
        _dbConnString = TestConfig.GetTestDbConnectionString();
    }

    [TestMethod]
    public void TestIdentityManagerGenericIdGeneration()
    {
        // Arrange - Create services with DI
        var services = new ServiceCollection();
        services.AddObjectIdentity(options =>
        {
            options.ConnectionString = _dbConnString;
            options.TableSchema = "dbo";
            options.DefaultBlockSize = 10;
        });
        var provider = services.BuildServiceProvider();
        var identityManager = provider.GetRequiredService<IdentityManager>();
        
        // Generate unique type names to avoid conflicts with other tests
        string typeName = $"TestEntity_{Guid.NewGuid():N}";
        
        try
        {
            // Act - Get IDs using the generic method
            int firstId = identityManager.GetNextIdentity<int>(typeName);
            int secondId = identityManager.GetNextIdentity<int>(typeName);
            int thirdId = identityManager.GetNextIdentity<int>(typeName);
            
            // Assert - Verify sequential IDs
            Assert.AreEqual(1, firstId);
            Assert.AreEqual(2, secondId);
            Assert.AreEqual(3, thirdId);
            
            // Also test with a specific starting ID
            string typeName2 = $"TestEntity_{Guid.NewGuid():N}";
            identityManager.IntializeScope<long>(typeName2, 1000);
            
            long id1 = identityManager.GetNextIdentity<long>(typeName2);
            long id2 = identityManager.GetNextIdentity<long>(typeName2);
            
            Assert.AreEqual(1000, id1);
            Assert.AreEqual(1001, id2);
        }
        finally
        {
            // Clean up - nothing to do as IdentityManager handles cleanup
        }
    }
    
    [TestMethod]
    public void TestMultipleTypeIdentities()
    {
        // Arrange - Set up the IdentityManager using dependency injection
        var services = new ServiceCollection();
        services.AddObjectIdentity(options =>
        {
            options.ConnectionString = _dbConnString;
            options.TableSchema = "dbo";
            options.DefaultBlockSize = 5;
        });
        var provider = services.BuildServiceProvider();
        var identityManager = provider.GetRequiredService<IdentityManager>();
        
        // Generate unique type names to avoid conflicts with other tests
        string entityName = $"Entity_{Guid.NewGuid():N}";
        
        try
        {
            // Act - Get different types of IDs for the same entity
            int intId1 = identityManager.GetNextIdentity<int>(entityName);
            int intId2 = identityManager.GetNextIdentity<int>(entityName);
            
            long longId1 = identityManager.GetNextIdentity<long>(entityName + "_Long");
            long longId2 = identityManager.GetNextIdentity<long>(entityName + "_Long");
            
            // Assert - Verify correct types and sequential values
            Assert.IsInstanceOfType(intId1, typeof(int));
            Assert.IsInstanceOfType(longId1, typeof(long));
            
            Assert.AreEqual(1, intId1);
            Assert.AreEqual(2, intId2);
            Assert.AreEqual(1, longId1);
            Assert.AreEqual(2, longId2);
        }
        finally
        {
            // Clean up - nothing to do as IdentityManager handles cleanup
        }
    }
    
    [TestMethod]
    public async Task TestAsyncIdentityGeneration()
    {
        // Arrange - Set up the IdentityManager
        var services = new ServiceCollection();
        services.AddObjectIdentity(options =>
        {
            options.ConnectionString = _dbConnString;
            options.TableSchema = "dbo";
            options.DefaultBlockSize = 10;
        });
        var provider = services.BuildServiceProvider();
        var identityManager = provider.GetRequiredService<IdentityManager>();
        
        // Generate unique type name to avoid conflicts with other tests
        string typeName = $"AsyncEntity_{Guid.NewGuid():N}";
        
        try
        {
            // Act - Get IDs using the async method
            int id1 = await identityManager.GetNextIdentityAsync<int>(typeName);
            int id2 = await identityManager.GetNextIdentityAsync<int>(typeName);
            int id3 = await identityManager.GetNextIdentityAsync<int>(typeName);
            
            // Assert - Verify sequential IDs
            Assert.AreEqual(1, id1);
            Assert.AreEqual(2, id2);
            Assert.AreEqual(3, id3);
        }
        finally
        {
            // Clean up - nothing to do as IdentityManager handles cleanup
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