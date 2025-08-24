using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ObjectIdentity;
using System;
using System.Threading.Tasks;

namespace ObjectIdentity.Tests;

/// <summary>
/// Tests for multiple identity manager configurations in the same project
/// </summary>
[TestClass]
public class MultipleConfigurationTests
{
    private static string _dbConnString = "";

    // Define custom interfaces for different identity managers
    public interface IPrimaryIdentityManager : IIdentityManager { }
    public interface ISecondaryIdentityManager : IIdentityManager { }
    public interface ICustomIdentityManager : IIdentityManager { }

    // Define custom implementations
    public class PrimaryIdentityManager : IdentityManager, IPrimaryIdentityManager
    {
        public PrimaryIdentityManager(IIdentityFactory factory) : base(factory) { }
    }

    public class SecondaryIdentityManager : IdentityManager, ISecondaryIdentityManager
    {
        public SecondaryIdentityManager(IIdentityFactory factory) : base(factory) { }
    }

    public class CustomIdentityManager : IdentityManager, ICustomIdentityManager
    {
        public string CustomProperty { get; set; } = "Custom";
        public CustomIdentityManager(IIdentityFactory factory) : base(factory) { }
    }

    [ClassInitialize]
    public static void Setup(TestContext context)
    {
        _dbConnString = TestConfig.GetTestDbConnectionString();
    }

    [TestMethod]
    public void TestMultipleIdentityManagerConfigurations()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register primary identity manager with one configuration
        services.AddObjectIdentity<IPrimaryIdentityManager, PrimaryIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "PrimaryIdentity";
                options.DefaultBlockSize = 10;
            });

        // Register secondary identity manager with different configuration
        services.AddObjectIdentity<ISecondaryIdentityManager, SecondaryIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "SecondaryIdentity";
                options.DefaultBlockSize = 20;
            });

        var provider = services.BuildServiceProvider();

        // Act
        var primaryManager = provider.GetRequiredService<IPrimaryIdentityManager>();
        var secondaryManager = provider.GetRequiredService<ISecondaryIdentityManager>();

        // Generate unique scope names
        string primaryScope = $"PrimaryTest_{Guid.NewGuid():N}";
        string secondaryScope = $"SecondaryTest_{Guid.NewGuid():N}";

        // Get IDs from each manager
        int primaryId1 = primaryManager.GetNextIdentity<int>(primaryScope);
        int primaryId2 = primaryManager.GetNextIdentity<int>(primaryScope);
        
        int secondaryId1 = secondaryManager.GetNextIdentity<int>(secondaryScope);
        int secondaryId2 = secondaryManager.GetNextIdentity<int>(secondaryScope);

        // Assert
        Assert.IsNotNull(primaryManager);
        Assert.IsNotNull(secondaryManager);
        Assert.AreNotSame((object)primaryManager, (object)secondaryManager, "Managers should be different instances");
        
        Assert.AreEqual(1, primaryId1);
        Assert.AreEqual(2, primaryId2);
        
        Assert.AreEqual(1, secondaryId1);
        Assert.AreEqual(2, secondaryId2);
    }

    [TestMethod]
    public async Task TestMultipleManagersWithDifferentScopesAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register multiple managers
        services.AddObjectIdentity<IPrimaryIdentityManager, PrimaryIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "Primary";
                options.DefaultBlockSize = 5;
            });

        services.AddObjectIdentity<ISecondaryIdentityManager, SecondaryIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "Secondary";
                options.DefaultBlockSize = 10;
            });

        var provider = services.BuildServiceProvider();
        var primaryManager = provider.GetRequiredService<IPrimaryIdentityManager>();
        var secondaryManager = provider.GetRequiredService<ISecondaryIdentityManager>();

        // Act - Use same scope name but different managers (simulating different databases/schemas)
        string sharedScopeName = $"SharedScope_{Guid.NewGuid():N}";
        
        // Initialize with different starting values
        primaryManager.IntializeScope<long>(sharedScopeName, 1000);
        secondaryManager.IntializeScope<long>(sharedScopeName, 2000);

        // Get IDs asynchronously
        long primaryId = await primaryManager.GetNextIdentityAsync<long>(sharedScopeName);
        long secondaryId = await secondaryManager.GetNextIdentityAsync<long>(sharedScopeName);

        // Assert
        Assert.AreEqual(1000L, primaryId, "Primary manager should start from 1000");
        Assert.AreEqual(2000L, secondaryId, "Secondary manager should start from 2000");
    }

    [TestMethod]
    public void TestCustomImplementationFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register with custom factory that sets custom properties
        services.AddObjectIdentity<ICustomIdentityManager, CustomIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "Custom";
            },
            provider =>
            {
                // Since SqlIdentityStore and IdentityFactory are internal,
                // we need to use the service provider to create them
                var tempServices = new ServiceCollection();
                tempServices.Configure<ObjectIdentityOptions>(options =>
                {
                    options.ConnectionString = _dbConnString;
                    options.TableSchema = "dbo";
                    options.IdentitySchema = "Custom";
                });
                tempServices.AddObjectIdentity(opts =>
                {
                    opts.ConnectionString = _dbConnString;
                    opts.TableSchema = "dbo";
                    opts.IdentitySchema = "Custom";
                });
                var tempProvider = tempServices.BuildServiceProvider();
                var factory = tempProvider.GetRequiredService<IIdentityFactory>();
                return new CustomIdentityManager(factory) { CustomProperty = "Modified" };
            });

        var provider = services.BuildServiceProvider();

        // Act
        var customManager = provider.GetRequiredService<ICustomIdentityManager>();
        string scopeName = $"CustomTest_{Guid.NewGuid():N}";
        int id = customManager.GetNextIdentity<int>(scopeName);

        // Assert
        Assert.IsNotNull(customManager);
        Assert.IsInstanceOfType(customManager, typeof(CustomIdentityManager));
        Assert.AreEqual("Modified", ((CustomIdentityManager)customManager).CustomProperty);
        Assert.AreEqual(1, id);
    }

    [TestMethod]
    public void TestMixedRegistrationWithDefaultAndCustom()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register default identity manager
        services.AddObjectIdentity(options =>
        {
            options.ConnectionString = _dbConnString;
            options.TableSchema = "dbo";
            options.DefaultBlockSize = 15;
        });

        // Also register custom identity manager
        services.AddObjectIdentity<IPrimaryIdentityManager, PrimaryIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "CustomSchema";
                options.DefaultBlockSize = 25;
            });

        var provider = services.BuildServiceProvider();

        // Act
        var defaultManager = provider.GetRequiredService<IIdentityManager>();
        var defaultManagerConcrete = provider.GetRequiredService<IdentityManager>();
        var customManager = provider.GetRequiredService<IPrimaryIdentityManager>();

        string scope1 = $"DefaultTest_{Guid.NewGuid():N}";
        string scope2 = $"CustomTest_{Guid.NewGuid():N}";

        int defaultId = defaultManager.GetNextIdentity<int>(scope1);
        int customId = customManager.GetNextIdentity<int>(scope2);

        // Assert
        Assert.IsNotNull(defaultManager);
        Assert.IsNotNull(defaultManagerConcrete);
        Assert.IsNotNull(customManager);
        Assert.AreSame(defaultManager, defaultManagerConcrete, "Default registrations should be the same instance");
        Assert.AreNotSame((object)defaultManager, (object)customManager, "Default and custom should be different instances");
        Assert.AreEqual(1, defaultId);
        Assert.AreEqual(1, customId);
    }

    [TestMethod]
    public void TestIsolationBetweenMultipleManagers()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddObjectIdentity<IPrimaryIdentityManager, PrimaryIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "IsolationTest1";
            });

        services.AddObjectIdentity<ISecondaryIdentityManager, SecondaryIdentityManager>(
            options =>
            {
                options.ConnectionString = _dbConnString;
                options.TableSchema = "dbo";
                options.IdentitySchema = "IsolationTest2";
            });

        var provider = services.BuildServiceProvider();
        var primary = provider.GetRequiredService<IPrimaryIdentityManager>();
        var secondary = provider.GetRequiredService<ISecondaryIdentityManager>();

        // Act - Use the same scope name in both managers
        string commonScope = $"CommonScope_{Guid.NewGuid():N}";
        
        // Initialize both with different starting points
        primary.IntializeScope<int>(commonScope, 100);
        secondary.IntializeScope<int>(commonScope, 500);

        // Get multiple IDs from each
        int[] primaryIds = new int[3];
        int[] secondaryIds = new int[3];
        
        for (int i = 0; i < 3; i++)
        {
            primaryIds[i] = primary.GetNextIdentity<int>(commonScope);
            secondaryIds[i] = secondary.GetNextIdentity<int>(commonScope);
        }

        // Assert - Each manager maintains its own sequence
        Assert.AreEqual(100, primaryIds[0]);
        Assert.AreEqual(101, primaryIds[1]);
        Assert.AreEqual(102, primaryIds[2]);
        
        Assert.AreEqual(500, secondaryIds[0]);
        Assert.AreEqual(501, secondaryIds[1]);
        Assert.AreEqual(502, secondaryIds[2]);
    }
}