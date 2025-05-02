namespace ObjectIdentity;

/// <summary>
/// Defines the interface for a storage provider that manages and retrieves blocks of sequential IDs.
/// </summary>
/// <remarks>
/// The identity store is responsible for interacting with the underlying storage system
/// (typically a SQL database) to initialize identity sequences, check for their existence,
/// and retrieve blocks of sequential IDs for different scopes.
/// </remarks>
public interface IIdentityStore
{
    /// <summary>
    /// Initializes the identity store by ensuring required database objects exist.
    /// </summary>
    /// <remarks>
    /// This method should be called before using the store to ensure that any
    /// required database objects (schemas, tables, etc.) have been created.
    /// </remarks>
    void Initialize();
    
    /// <summary>
    /// Initializes the identity store asynchronously by ensuring required database objects exist.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <remarks>
    /// This is the asynchronous version of <see cref="Initialize"/> and is recommended
    /// for use in asynchronous applications.
    /// </remarks>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a specific scope has been initialized in the storage system.
    /// </summary>
    /// <param name="scope">The name of the scope to check.</param>
    /// <returns><c>true</c> if the scope has been initialized; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// A scope is considered initialized when a corresponding sequence or other
    /// storage mechanism exists for generating IDs for that scope.
    /// </remarks>
    bool IsInitialized(string? scope);
    
    /// <summary>
    /// Checks asynchronously if a specific scope has been initialized in the storage system.
    /// </summary>
    /// <param name="scope">The name of the scope to check.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result is <c>true</c> if the scope has been initialized; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is the asynchronous version of <see cref="IsInitialized"/> and is recommended
    /// for use in asynchronous applications.
    /// </remarks>
    Task<bool> IsInitializedAsync(string? scope, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the next block of sequential IDs for the specified scope.
    /// </summary>
    /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
    /// <param name="scope">The name of the scope to get IDs for.</param>
    /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
    /// <param name="startingId">Optional starting ID value for a new scope.</param>
    /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
    /// <returns>A list of sequential ID values of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// This method retrieves a block of sequential IDs from the storage system,
    /// which can then be handed out individually without requiring additional database calls.
    /// </remarks>
    List<T> GetNextIdBlock<T>(string? scope, int blockSize, long? startingId = null, long? maxValue = null) 
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    
    /// <summary>
    /// Gets the next block of sequential IDs for the specified scope asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
    /// <param name="scope">The name of the scope to get IDs for.</param>
    /// <param name="blockSize">The number of IDs to retrieve in this block.</param>
    /// <param name="startingId">Optional starting ID value for a new scope.</param>
    /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a list of sequential ID values of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// This is the asynchronous version of <see cref="GetNextIdBlock{T}"/> and is recommended
    /// for use in asynchronous applications.
    /// </remarks>
    Task<List<T>> GetNextIdBlockAsync<T>(string? scope, int blockSize, long? startingId = null, long? maxValue = null, CancellationToken cancellationToken = default)
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    
    /// <summary>
    /// Gets the recommended starting ID for a new scope based on existing data.
    /// </summary>
    /// <param name="scope">The name of the scope to check.</param>
    /// <returns>The recommended starting ID value, typically based on existing data with a safety buffer.</returns>
    /// <remarks>
    /// This method checks existing tables/data to determine an appropriate starting value
    /// for a new scope, typically by finding the maximum existing ID and adding a safety buffer.
    /// </remarks>
    long GetInitialStartValueForScope(string? scope);
    
    /// <summary>
    /// Gets the recommended starting ID for a new scope based on existing data asynchronously.
    /// </summary>
    /// <param name="scope">The name of the scope to check.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the recommended starting ID value.</returns>
    /// <remarks>
    /// This is the asynchronous version of <see cref="GetInitialStartValueForScope"/> and is recommended
    /// for use in asynchronous applications.
    /// </remarks>
    Task<long> GetInitialStartValueForScopeAsync(string? scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes a scope and returns a function to get ID blocks.
    /// </summary>
    /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
    /// <param name="scope">The name of the scope to initialize.</param>
    /// <param name="startingId">Optional starting ID value for the scope.</param>
    /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
    /// <returns>A function that takes a block size and returns a list of sequential IDs.</returns>
    /// <remarks>
    /// This method creates or ensures the existence of a scope in the storage system,
    /// and returns a delegate that can be called to retrieve blocks of IDs for that scope.
    /// </remarks>
    Func<int, List<T>> Initialize<T>(string? scope, long? startingId = null, long? maxValue = null)
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
        
    /// <summary>
    /// Initializes a scope asynchronously and returns a function to get ID blocks asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
    /// <param name="scope">The name of the scope to initialize.</param>
    /// <param name="startingId">Optional starting ID value for the scope.</param>
    /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a function that takes a block size and returns a task yielding a list of sequential IDs.</returns>
    /// <remarks>
    /// This is the asynchronous version of <see cref="Initialize{T}"/> and is recommended
    /// for use in asynchronous applications.
    /// </remarks>
    Task<Func<int, Task<List<T>>>> InitializeAsync<T>(string? scope, long? startingId = null, long? maxValue = null, CancellationToken cancellationToken = default)
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
}
