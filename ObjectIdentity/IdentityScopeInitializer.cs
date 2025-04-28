

namespace ObjectIdentity;

public class IdentityScopeInitializer : IIdentityScopeInitializer
{
    private readonly IIdentityStore _store;
    
    public IdentityScopeInitializer(IIdentityStore store)
    {
        _store = store;
        _store.Initialize();
    }
    
    public void Initialize()
    {
        _store.Initialize();
    }
    
    public Func<int, List<T>> Initialize<T>(string scope, long? startingId = null, long? maxValue = null)
        where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        return blockSize => _store.GetNextIdBlock<T>(scope, blockSize, startingId, maxValue);
    }
}
