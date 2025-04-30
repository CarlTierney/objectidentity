using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ObjectIdentity;

/// <summary>
/// Contains extension methods for configuring ObjectIdentity services in an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// These extension methods make it easy to integrate ObjectIdentity with dependency injection
/// systems that implement <see cref="IServiceCollection"/>, such as ASP.NET Core.
/// </remarks>
public static class ObjectIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQL-based ObjectIdentity services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="ObjectIdentityOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// This method registers the core ObjectIdentity services with the dependency injection container:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="IIdentityStore"/> implemented by <see cref="SqlIdentityStore"/></description></item>
    ///   <item><description><see cref="IIdentityFactory"/> implemented by <see cref="IdentityFactory"/></description></item>
    ///   <item><description><see cref="IdentityManager"/> as the main entry point for ID generation</description></item>
    /// </list>
    /// <para>
    /// Example usage:
    /// </para>
    /// <code>
    /// services.AddObjectIdentity(options =>
    /// {
    ///     options.ConnectionString = configuration.GetConnectionString("DefaultConnection");
    ///     options.DefaultBlockSize = 200;
    /// });
    /// </code>
    /// </remarks>
    public static IServiceCollection AddObjectIdentity(this IServiceCollection services, Action<ObjectIdentityOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IIdentityStore, SqlIdentityStore>();
        services.AddSingleton<IIdentityFactory, IdentityFactory>();
        services.AddSingleton<IdentityManager>();
        
        return services;
    }

    /// <summary>
    /// Adds SQL-based ObjectIdentity services to the specified <see cref="IServiceCollection"/> with a custom identity factory.
    /// </summary>
    /// <typeparam name="TIdentityFactory">The type of the custom identity factory implementation.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="ObjectIdentityOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// Similar to <see cref="AddObjectIdentity(IServiceCollection, Action{ObjectIdentityOptions})"/>, 
    /// but allows you to specify a custom implementation of <see cref="IIdentityFactory"/>.
    /// Use this method when you need to customize how identity scopes are created.
    /// </para>
    /// <para>
    /// Example usage with a custom factory:
    /// </para>
    /// <code>
    /// services.AddObjectIdentity&lt;CustomIdentityFactory&gt;(options =>
    /// {
    ///     options.ConnectionString = configuration.GetConnectionString("DefaultConnection");
    ///     options.DefaultBlockSize = 200;
    /// });
    /// </code>
    /// </remarks>
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
