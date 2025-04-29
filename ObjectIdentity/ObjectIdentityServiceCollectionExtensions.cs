using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ObjectIdentity;

public static class ObjectIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQL-based ObjectIdentity services to the specified IServiceCollection
    /// </summary>
    public static IServiceCollection AddObjectIdentity(this IServiceCollection services, Action<ObjectIdentityOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IIdentityStore, SqlIdentityStore>();
        services.AddSingleton<IIdentityFactory, IdentityFactory>();
        services.AddSingleton<IdentityManager>();
        
        return services;
    }

    /// <summary>
    /// Adds SQL-based ObjectIdentity services to the specified IServiceCollection with a custom identity factory
    /// </summary>
    public static IServiceCollection AddObjectIdentity<TIdentityFactory>(this IServiceCollection services, Action<ObjectIdentityOptions> configureOptions) 
        where TIdentityFactory : class, IIdentityFactory
    {
        services.Configure(configureOptions);
        services.AddSingleton<IIdentityStore, SqlIdentityStore>();
        services.AddSingleton<IIdentityFactory, TIdentityFactory>();
        services.AddSingleton<IdentityManager>();
        
        return services;
    }
}
