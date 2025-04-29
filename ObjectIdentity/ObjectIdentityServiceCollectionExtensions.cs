using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ObjectIdentity;

public static class ObjectIdentityServiceCollectionExtensions
{
   

    public static IServiceCollection AddObjectIdentity(this IServiceCollection services, Action<ObjectIdentityOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IIdentityScopeInitializer, SqlIdentityScopeInitializer>();
        services.AddSingleton<IIdentityFactory, IdentityScopeFactory>();
        services.AddSingleton<IdentityManager>();
        return services;
    }
}
