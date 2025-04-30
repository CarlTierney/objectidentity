// Add options class for configuration
namespace ObjectIdentity;

/// <summary>
/// Configuration options for the ObjectIdentity library.
/// </summary>
/// <remarks>
/// This class provides configuration settings that control the behavior of the 
/// ObjectIdentity components, particularly the SqlIdentityStore and related services.
/// </remarks>
public class ObjectIdentityOptions
{  
    /// <summary>
    /// Gets or sets the database connection string for the identity store.
    /// </summary>
    /// <remarks>
    /// This connection string is used by SqlIdentityStore to connect to the SQL Server database.
    /// </remarks>
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the database schema where tables with identity columns are located.
    /// </summary>
    /// <remarks>
    /// When determining initial ID values, the system will look for tables with matching names
    /// in this schema. The default value is "dbo".
    /// </remarks>
    public string TableSchema { get; set; } = "dbo";
    
    /// <summary>
    /// Gets or sets the database schema where identity sequences will be created.
    /// </summary>
    /// <remarks>
    /// SQL sequences used for ID generation will be created in this schema.
    /// The default value is "ids".
    /// </remarks>
    public string IdentitySchema { get; set; } = "ids";
    
    /// <summary>
    /// Gets or sets a value indicating whether object names should be pluralized.
    /// </summary>
    /// <remarks>
    /// When true, scope names will be pluralized when converted to table or sequence names.
    /// For example, "Customer" would become "Customers". The default value is false.
    /// </remarks>
    public bool IsObjectNamePlural { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the column name used in the IdFactory table to identify object types.
    /// </summary>
    /// <remarks>
    /// This is the column name in the IdFactory table that contains the object or type names.
    /// The default value is "ObjectName".
    /// </remarks>
    public string IdFactoryObjectOrTypeName { get; set; } = "ObjectName";
    
    /// <summary>
    /// Gets or sets the column name used for identity values in entity tables.
    /// </summary>
    /// <remarks>
    /// When determining the maximum existing ID in tables, this column name will be used.
    /// The default value is "Id".
    /// </remarks>
    public string IdentityColName { get; set; } = "Id";
    
    /// <summary>
    /// Gets or sets the default block size for ID generation.
    /// </summary>
    /// <remarks>
    /// This value determines how many IDs are retrieved in a single batch from the database.
    /// A larger block size reduces database calls but may waste more IDs in case of application
    /// restarts. The default value is 100.
    /// </remarks>
    public int DefaultBlockSize { get; set; } = 100;
}
