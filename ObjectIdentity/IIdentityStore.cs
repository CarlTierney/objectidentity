namespace ObjectIdentity;

public interface IIdentityStore
{
    /// <summary>
    /// Initializes the identity store
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Initializes the identity store asynchronously
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a specific scope has been initialized
    /// </summary>
    bool IsInitialized(string scope);
    
    /// <summary>
    /// Checks if a specific scope has been initialized asynchronously
    /// </summary>
    Task<bool> IsInitializedAsync(string scope, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the next block of sequential IDs for the specified scope
    /// </summary>
    List<T> GetNextIdBlock<T>(string scope, int blockSize, long? startingId = null, long? maxValue = null) 
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    
    /// <summary>
    /// Gets the next block of sequential IDs for the specified scope asynchronously
    /// </summary>
    Task<List<T>> GetNextIdBlockAsync<T>(string scope, int blockSize, long? startingId = null, long? maxValue = null, CancellationToken cancellationToken = default)
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    
    /// <summary>
    /// Gets the recommended starting ID for a scope
    /// </summary>
    long GetInitialStartValueForScope(string scope);
    
    /// <summary>
    /// Gets the recommended starting ID for a scope asynchronously
    /// </summary>
    Task<long> GetInitialStartValueForScopeAsync(string scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes a scope and returns a function to get ID blocks
    /// </summary>
    Func<int, List<T>> Initialize<T>(string scope, long? startingId = null, long? maxValue = null)
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
        
    /// <summary>
    /// Initializes a scope and returns a function to get ID blocks asynchronously
    /// </summary>
    Task<Func<int, Task<List<T>>>> InitializeAsync<T>(string scope, long? startingId = null, long? maxValue = null, CancellationToken cancellationToken = default)
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
}
