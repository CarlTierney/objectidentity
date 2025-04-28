namespace ObjectIdentity;

public interface IIdentityStore
{
    /// <summary>
    /// Initializes the identity store
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Checks if a specific scope has been initialized
    /// </summary>
    bool IsInitialized(string scope);
    
    /// <summary>
    /// Gets the next block of sequential IDs for the specified scope
    /// </summary>
    List<T> GetNextIdBlock<T>(string scope, int blockSize, long? startingId = null, long? maxValue = null) 
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    
    /// <summary>
    /// Gets the recommended starting ID for a scope
    /// </summary>
    long GetInitialStartValueForScope(string scope);
}
