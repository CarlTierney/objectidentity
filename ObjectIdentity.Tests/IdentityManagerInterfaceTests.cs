using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ObjectIdentity;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace ObjectIdentity.Tests;

/// <summary>
/// Tests to verify IIdentityManager interface works correctly and can be mocked
/// </summary>
[TestClass]
public class IdentityManagerInterfaceTests
{
    private static string _dbConnString = "";

    [ClassInitialize]
    public static void Setup(TestContext context)
    {
        _dbConnString = TestConfig.GetTestDbConnectionString();
    }

    [TestMethod]
    public void TestIdentityManagerCanBeResolvedViaInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectIdentity(options =>
        {
            options.ConnectionString = _dbConnString;
            options.TableSchema = "dbo";
            options.DefaultBlockSize = 10;
        });
        var provider = services.BuildServiceProvider();

        // Act
        var identityManagerViaInterface = provider.GetRequiredService<IIdentityManager>();
        var identityManagerDirect = provider.GetRequiredService<IdentityManager>();

        // Assert
        Assert.IsNotNull(identityManagerViaInterface);
        Assert.IsNotNull(identityManagerDirect);
        Assert.AreSame(identityManagerViaInterface, identityManagerDirect, "Both should resolve to the same singleton instance");
    }

    [TestMethod]
    public void TestIdentityManagerInterfaceOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectIdentity(options =>
        {
            options.ConnectionString = _dbConnString;
            options.TableSchema = "dbo";
            options.DefaultBlockSize = 10;
        });
        var provider = services.BuildServiceProvider();
        var identityManager = provider.GetRequiredService<IIdentityManager>();
        
        string scopeName = $"InterfaceTest_{Guid.NewGuid():N}";

        // Act & Assert - Test synchronous operations
        int firstId = identityManager.GetNextIdentity<int>(scopeName);
        int secondId = identityManager.GetNextIdentity<int>(scopeName);
        
        Assert.AreEqual(1, firstId);
        Assert.AreEqual(2, secondId);

        // Test scope initialization
        string scopeName2 = $"InterfaceTest2_{Guid.NewGuid():N}";
        identityManager.IntializeScope<long>(scopeName2, 100);
        long longId = identityManager.GetNextIdentity<long>(scopeName2);
        
        Assert.AreEqual(100L, longId);
    }

    [TestMethod]
    public async Task TestIdentityManagerInterfaceAsyncOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectIdentity(options =>
        {
            options.ConnectionString = _dbConnString;
            options.TableSchema = "dbo";
            options.DefaultBlockSize = 10;
        });
        var provider = services.BuildServiceProvider();
        var identityManager = provider.GetRequiredService<IIdentityManager>();
        
        string scopeName = $"InterfaceAsyncTest_{Guid.NewGuid():N}";

        // Act & Assert - Test asynchronous operations
        int firstId = await identityManager.GetNextIdentityAsync<int>(scopeName);
        int secondId = await identityManager.GetNextIdentityAsync<int>(scopeName);
        
        Assert.AreEqual(1, firstId);
        Assert.AreEqual(2, secondId);
    }

    [TestMethod]
    public void TestMockableIdentityManager()
    {
        // This test demonstrates that IIdentityManager can be easily mocked for unit testing
        var mockIdentityManager = new MockIdentityManager();
        
        // Act
        int id1 = mockIdentityManager.GetNextIdentity<int>("test");
        int id2 = mockIdentityManager.GetNextIdentity<int>("test");
        long id3 = mockIdentityManager.GetNextIdentity<long>("test2");
        
        // Assert
        Assert.AreEqual(1, id1);
        Assert.AreEqual(2, id2);
        Assert.AreEqual(1L, id3);
    }

    /// <summary>
    /// Example mock implementation of IIdentityManager for unit testing
    /// </summary>
    private class MockIdentityManager : IIdentityManager
    {
        private readonly Dictionary<string, Dictionary<Type, long>> _scopes = new();
        private readonly object _lock = new();

        public void IntializeScope<T>(string? scopeName, int startingId) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            lock (_lock)
            {
                if (!_scopes.ContainsKey(scopeName ?? ""))
                    _scopes[scopeName ?? ""] = new Dictionary<Type, long>();
                
                _scopes[scopeName ?? ""][typeof(T)] = startingId;
            }
        }

        public void InitializeScope<TScope, T>(int startingId) 
            where TScope : class 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            IntializeScope<T>(typeof(TScope).Name, startingId);
        }

        public T GetNextIdentity<TScope, T>() 
            where TScope : class 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return GetNextIdentity<T>(typeof(TScope).Name);
        }

        public T GetNextIdentity<T>(string? objectName) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            lock (_lock)
            {
                string key = objectName ?? "";
                if (!_scopes.ContainsKey(key))
                    _scopes[key] = new Dictionary<Type, long>();
                
                if (!_scopes[key].ContainsKey(typeof(T)))
                    _scopes[key][typeof(T)] = 1;
                else
                    _scopes[key][typeof(T)]++;
                
                return (T)Convert.ChangeType(_scopes[key][typeof(T)], typeof(T));
            }
        }

        public Task<T> GetNextIdentityAsync<TScope, T>(CancellationToken cancellationToken = default) 
            where TScope : class 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return Task.FromResult(GetNextIdentity<TScope, T>());
        }

        public Task<T> GetNextIdentityAsync<T>(string? objectName, CancellationToken cancellationToken = default) 
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return Task.FromResult(GetNextIdentity<T>(objectName));
        }
    }
}