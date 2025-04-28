# ObjectIdentity

A library for generating unique IDs for objects prior to their being saved to the database with SQL Server backend support via sequences.

## Features

- Thread-safe unique ID generation
- SQL Server backend
- Extensible to support alternative stores for mechanisms via the IIdentityStore interface
- Support 
- Configurable ID ranges
- Efficient batch allocation, allocation of block sizes can be handled via the scope initializer
- Designed to be able to handle existing data by allowing the sequences to be configured with a starting id
- Intended to reduce round trips to the database by grabbing sets of ids at a time 
- Uses a background thread to retrieve additional ids prior to the id cache running out to prevent intermittiant pauses in id assignment

## Usage


### Basic Setup

The ObjectIdentity library provides a flexible system for generating unique IDs with a SQL Server backend. Start by initializing the core components:

<pre><code class='language-cs'>
    // Create the initializer with your database connection 
  var initializer = new SqlIdentityScopeInitializer(connectionString, "dbo", false);
// Create the factory using the initializer 
  var factory = new IdentityScopeFactory(initializer);
// Create the identity manager that will generate IDs
  var manager = new IdentityManager(factory);
</code>
</pre>

### Getting IDs

Once configured, you can request IDs for your domain objects:

<pre><code class='language-cs'>
// Get the next available ID for an object such as LedgerTransaction
  long id = manager.GetNextIdentity<LedgerTransaction, long>();
</code>
</pre>

### Custom Starting Values

You can initialize scopes with specific starting values:

<pre><code class='language-cs'>
    // Initialize a scope with a specific starting ID
    manager.IntializeScope ("CustomScope", 1000); 
    // Or use a type to define the scope 
    manager.InitializeScope<MyEntityType, long>(1000);
    // Get IDs from the initialized 
    scope long id = manager.GetNextIdentity<MyEntityType,long>();
</code>
</pre>
### Concurrent Usage

The library is designed for thread-safe operation in multi-threaded environments:
csharp // Create tasks that generate IDs concurrently var tasks = new List<Task<List >>(); 
for (var i = 0; i < 10; i++) { var task = Task.Run(() => { var ids = new List (); for (var j = 0; j < 1000; j++) { ids. Add(manager. GetNextIdentity<MyEntity, long>()); } return ids; }); 
tasks.Add(task);
}
// Wait for all tasks to complete Task.WaitAll(tasks);
// All generated IDs are guaranteed to be unique across threads

### Configuration Options

When creating the SQL initializer, you can customize various aspects:
csharp var initializer = new SqlIdentityScopeInitializer( connectionString, // Your database connection string ;

### Complete Example

csharp // Setup configuration to get connection string var config = new ConfigurationBuilder() .AddJsonFile("appsettings.json") .AddEnvironmentVariables() .Build();
string connectionString = config.GetConnectionString("MyDatabase");
// Initialize components var initializer = new SqlIdentityScopeInitializer(connectionString, "dbo", false); var factory = new IdentityScopeFactory(initializer); var manager = new IdentityManager(factory);
// Initialize scope with starting ID manager.InitializeScope<Customer, long>(1000);
// Generate IDs for new entities var customer = new Customer { Id = manager.GetNextIdentity<Customer, long>(), Name = "New Customer" };
// IDs can be generated in batches for performance var batchOfIds = new List (); for (int i = 0; i < 100; i++) { batchOfIds. Add(manager. GetNextIdentity<Order, long>()); }
