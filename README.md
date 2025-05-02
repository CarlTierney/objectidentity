# ObjectIdentity

[![Build and Publish](https://github.com/CarlTierney/ObjectIdentity/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/YourUsername/ObjectIdentity/actions/workflows/build-and-publish.yml)
[![NuGet Version](https://img.shields.io/nuget/v/ObjectIdentity.svg)](https://www.nuget.org/packages/ObjectIdentity/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ObjectIdentity.svg)](https://www.nuget.org/packages/ObjectIdentity/)

A library for generating unique IDs for objects prior to their being saved to the database with SQL Server backend support via sequences.

## Features

- Thread-safe unique ID generation
- SQL Server backend
- Extensible to support alternative stores for mechanisms via the IIdentityStore interface
- Configurable ID ranges
- Efficient batch allocation, allocation of block sizes can be handled via the scope initializer
- Designed to be able to handle existing data by allowing the sequences to be configured with a starting id
- Intended to reduce round trips to the database by grabbing sets of ids at a time 
- Uses a background thread to retrieve additional ids prior to the id cache running out to prevent intermittiant pauses in id assignment

## Usage

### Dependency Injection Setup (Recommended)

ObjectIdentity now supports Microsoft.Extensions.DependencyInjection for easy integration:

<pre><code class='language-cs'>
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
var manager = provider.GetRequiredService<IdentityManager>();

// Get the next available ID for an object such as LedgerTransaction
long id = manager.GetNextIdentity<LedgerTransaction, long>();
</code></pre>

### Basic Setup (Manual Construction)

You can still manually construct the components if you need more control:

<pre><code class='language-cs'>
using Microsoft.Extensions.Options;
using ObjectIdentity;

var options = Options.Create(new ObjectIdentityOptions {
    ConnectionString = "your-connection-string",
    TableSchema = "dbo"
});
var store = new SqlIdentityStore(options);
var factory = new IdentityFactory(store, options);
var manager = new IdentityManager(factory);
</code></pre>

### Getting IDs

Once configured, you can request IDs for your domain objects:

<pre><code class='language-cs'>
// Get the next available ID for an object such as LedgerTransaction
long id = manager.GetNextIdentity<LedgerTransaction, long>();
</code></pre>

### Custom Starting Values

You can initialize scopes with specific starting values:

<pre><code class='language-cs'>
// Initialize a scope with a specific starting ID
manager.IntializeScope<long>("CustomScope", 1000); 
// Or use a type to define the scope 
manager.InitializeScope<MyEntityType, long>(1000);
// Get IDs from the initialized scope
long id = manager.GetNextIdentity<MyEntityType, long>();
</code></pre>

### Concurrent Usage
The library is designed for thread-safe operation in multi-threaded environments:

<pre><code class='language-cs'>
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
</code></pre>

## Testing

This library is fully tested using SQL Server LocalDB for both local and CI/CD environments. The GitHub Actions workflow automatically sets up LocalDB, creates a test database, and runs the full test suite to ensure compatibility.