# ObjectIdentity

[![Build and Publish](https://github.com/CarlTierney/ObjectIdentity/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/YourUsername/ObjectIdentity/actions/workflows/build-and-publish.yml)
[![NuGet Version](https://img.shields.io/nuget/v/ObjectIdentity.svg)](https://www.nuget.org/packages/ObjectIdentity/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ObjectIdentity.svg)](https://www.nuget.org/packages/ObjectIdentity/)

A library for generating unique sequential IDs for objects across many servers and services prior to their being saved to the database.  An implementation exists using  a  SQL Server backend support via sequences, but the library is intended to be extensible to other potential back end soutions for identity.   

## Features

- Multi server, Thread-safe unique ID generation
- SQL Server backend
- Designed to support multiple instances of identity services without creating duplicate ids regardless of scale out
- Extensible to support alternative stores for mechanisms via the IIdentityStore interface
- Configurable ID ranges
- Efficient batch allocation, allocation of block sizes can be handled via the scope initializer
- Designed to be able to handle existing data by allowing the sequences to be configured with a starting id
- Intended to reduce round trips to the database by grabbing sets of ids at a time 
- Uses a background thread to retrieve additional ids prior to the id cache running out to prevent intermittiant pauses in id assignment

## Usage

### Dependency Injection Setup (Recommended)

ObjectIdentity now supports Microsoft.Extensions.DependencyInjection for easy integration:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ObjectIdentity;

var services = new ServiceCollection();
services.AddObjectIdentity(options =>
{
    options.ConnectionString = "your-connection-string";
    options.TableSchema = "dbo";
    // ...other options
});

var provider = services.BuildServiceProvider();

// You can resolve via the interface for easier mocking
var manager = provider.GetRequiredService<IIdentityManager>();

// Or via the concrete type if needed
var concreteManager = provider.GetRequiredService<IdentityManager>();

// Get the next available ID for an object such as LedgerTransaction
long id = manager.GetNextIdentity<LedgerTransaction, long>();
```

### Basic Setup (Manual Construction)

You can still manually construct the components if you need more control:

```csharp
using Microsoft.Extensions.Options;
using ObjectIdentity;

var options = Options.Create(new ObjectIdentityOptions {
    ConnectionString = "your-connection-string",
    TableSchema = "dbo"
});
var store = new SqlIdentityStore(options);
var factory = new IdentityFactory(store, options);
var manager = new IdentityManager(factory);
```

### Getting IDs

Once configured, you can request IDs for your domain objects:

```csharp
// Get the next available ID for an object such as LedgerTransaction
long id = manager.GetNextIdentity<LedgerTransaction, long>();
```

### Custom Starting Values

You can initialize scopes with specific starting values:

```csharp
// Initialize a scope with a specific starting ID
manager.IntializeScope<long>("CustomScope", 1000); 
// Or use a type to define the scope 
manager.InitializeScope<MyEntityType, long>(1000);
// Get IDs from the initialized scope
long id = manager.GetNextIdentity<MyEntityType, long>();
```

### Concurrent Usage
The library is designed for thread-safe operation in multi-threaded environments:

```csharp
// Create tasks that generate IDs concurrently
var tasks = new List<Task<List<long>>>(); 
for (var i = 0; i < 10; i++) 
{
    var task = Task.Run(() => 
    { 
        var ids = new List<long>(); 
        for (var j = 0; j < 1000; j++)
        { 
            ids.Add(manager.GetNextIdentity<MyEntity, long>()); 
        }
        return ids; 
    }); 
    tasks.Add(task);
}
// Wait for all tasks to complete 
Task.WaitAll(tasks.ToArray());
                        
// All generated IDs are guaranteed to be unique across threads
```

## Mocking and Testing

### Unit Testing with Mocks

The library now provides an `IIdentityManager` interface that makes it easy to mock identity generation in your unit tests:

```csharp
// Example using a mock identity manager for unit testing
public class MockIdentityManager : IIdentityManager
{
    private long _currentId = 0;
    
    public T GetNextIdentity<T>(string? objectName) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        return (T)Convert.ChangeType(++_currentId, typeof(T));
    }
    
    // Implement other interface methods as needed...
}

// In your test
var mockManager = new MockIdentityManager();
var service = new YourService(mockManager);
// Test your service logic without database dependency
```

### Multiple Configurations

You can register multiple identity managers with different configurations in the same project:

```csharp
// Define custom interfaces for different databases/schemas
public interface IPrimaryDbIdentityManager : IIdentityManager { }
public interface ISecondaryDbIdentityManager : IIdentityManager { }

// Create custom implementations
public class PrimaryDbIdentityManager : IdentityManager, IPrimaryDbIdentityManager
{
    public PrimaryDbIdentityManager(IIdentityFactory factory) : base(factory) { }
}

public class SecondaryDbIdentityManager : IdentityManager, ISecondaryDbIdentityManager
{
    public SecondaryDbIdentityManager(IIdentityFactory factory) : base(factory) { }
}

// Register both configurations
services.AddObjectIdentity<IPrimaryDbIdentityManager, PrimaryDbIdentityManager>(
    options =>
    {
        options.ConnectionString = "PrimaryDbConnection";
        options.IdentitySchema = "PrimaryIdentity";
    });

services.AddObjectIdentity<ISecondaryDbIdentityManager, SecondaryDbIdentityManager>(
    options =>
    {
        options.ConnectionString = "SecondaryDbConnection";
        options.IdentitySchema = "SecondaryIdentity";
    });

// Use them independently
var primaryManager = provider.GetRequiredService<IPrimaryDbIdentityManager>();
var secondaryManager = provider.GetRequiredService<ISecondaryDbIdentityManager>();
```

## Testing

This library is fully tested using SQL Server LocalDB for both local and CI/CD environments. The GitHub Actions workflow automatically sets up LocalDB, creates a test database, and runs the full test suite to ensure compatibility.