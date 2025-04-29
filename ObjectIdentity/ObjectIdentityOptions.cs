// Add options class for configuration
namespace ObjectIdentity;

public class ObjectIdentityOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TableSchema { get; set; } = "dbo";
    public string IdentitySchema { get; set; } = "ids";
    public bool IsObjectNamePlural { get; set; } = false;
    public string IdFactoryObjectOrTypeName { get; set; } = "ObjectName";
    public string IdentityColName { get; set; } = "Id";
    public int DefaultBlockSize { get; set; } = 100;
}
