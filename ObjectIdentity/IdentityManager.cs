using Pluralize.NET;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    /// <summary>
    /// Manages identity generation for different scopes in a thread-safe manner.
    /// Only one instance is needed per database connection.
    /// </summary>
    /// <remarks>
    /// The IdentityManager provides centralized identity generation with automatic scope initialization
    /// and caching for high-performance ID generation.
    /// </remarks>
    public class IdentityManager
    {
        private readonly object _registrationlock = new object();
        private readonly ConcurrentDictionary<string, IIdentityScope> _idScopes = new ConcurrentDictionary<string, IIdentityScope>();
        private readonly IIdentityFactory _defaultScopeFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityManager"/> class.
        /// </summary>
        /// <param name="scopeFactory">The factory used to create identity scopes.</param>
        public IdentityManager(IIdentityFactory scopeFactory)
        {
            _defaultScopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        /// <summary>
        /// Initializes a scope with the specified starting ID.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scopeName">The name of the scope to initialize.</param>
        /// <param name="startingId">The starting ID value for this scope.</param>
        /// <exception cref="ArgumentException">Thrown when the scope already exists.</exception>
        /// <remarks>
        /// Only use this when you specifically need to set the initial starting ID.
        /// The identity factory will automatically attempt to determine an appropriate starting value
        /// by checking the maximum existing value in the corresponding table.
        /// </remarks>
        public void IntializeScope<T>(string? scopeName, int startingId) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            lock (_registrationlock)
            {
                if (_idScopes.ContainsKey(scopeName))
                {
                    throw new ArgumentException($"Identity scope {scopeName} already exists for type {typeof(T).Name}");
                }

                var idScope = _defaultScopeFactory.CreateIdentityScope<T>(scopeName, startingId);
                _idScopes[scopeName] = idScope;
            }
        }

        /// <summary>
        /// Initializes a scope for the specified type with the given starting ID.
        /// </summary>
        /// <typeparam name="TScope">The type that defines the scope.</typeparam>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="startingId">The starting ID value for this scope.</param>
        /// <exception cref="ArgumentException">Thrown when the scope already exists.</exception>
        /// <remarks>
        /// Only use this when you specifically need to set the initial starting ID.
        /// The scope name is derived from the type name of <typeparamref name="TScope"/>.
        /// </remarks>
        public void InitializeScope<TScope, T>(int startingId) where TScope : class
                                                               where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var scopeName = typeof(TScope).Name;
            lock (_registrationlock)
            {
                if (_idScopes.ContainsKey(scopeName))
                {
                    throw new ArgumentException($"Identity scope {scopeName} already exists for type {typeof(T).Name}");
                }

                var idScope = _defaultScopeFactory.CreateIdentityScope<T>(scopeName, startingId);
                _idScopes[scopeName] = idScope;
            }
        }

        /// <summary>
        /// Gets the next identity value for the specified type scope.
        /// </summary>
        /// <typeparam name="TScope">The type that defines the scope.</typeparam>
        /// <typeparam name="T">The type of ID to generate (e.g., int, long).</typeparam>
        /// <returns>The next unique ID value.</returns>
        /// <remarks>
        /// Automatically initializes the scope if it doesn't exist by checking the database
        /// for the maximum value in the table with the same type name and adding a buffer to that max ID.
        /// </remarks>
        public T GetNextIdentity<TScope, T>() where TScope : class
                                              where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var scopeName = typeof(TScope).Name;
            return GetNextIdentityInternal<T>(scopeName);
        }

        /// <summary>
        /// Gets the next identity value for the specified scope name.
        /// </summary>
        /// <typeparam name="T">The type of ID to generate (e.g., int, long).</typeparam>
        /// <param name="objectName">The name of the scope.</param>
        /// <returns>The next unique ID value.</returns>
        /// <remarks>
        /// Automatically initializes the scope if it doesn't exist by checking the database
        /// for the maximum value in the table with the same name and adding a buffer to that max ID.
        /// </remarks>
        public T GetNextIdentity<T>(string? objectName) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return GetNextIdentityInternal<T>(objectName);
        }

        /// <summary>
        /// Gets the next identity value asynchronously for the specified type scope.
        /// </summary>
        /// <typeparam name="TScope">The type that defines the scope.</typeparam>
        /// <typeparam name="T">The type of ID to generate (e.g., int, long).</typeparam>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the next unique ID value.</returns>
        /// <remarks>
        /// Automatically initializes the scope if it doesn't exist by checking the database
        /// for the maximum value in the table with the same type name and adding a buffer to that max ID.
        /// </remarks>
        public async Task<T> GetNextIdentityAsync<TScope, T>(CancellationToken cancellationToken = default) where TScope : class
                                              where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var scopeName = typeof(TScope).Name;
            return await GetNextIdentityInternalAsync<T>(scopeName, cancellationToken);
        }

        /// <summary>
        /// Gets the next identity value asynchronously for the specified scope name.
        /// </summary>
        /// <typeparam name="T">The type of ID to generate (e.g., int, long).</typeparam>
        /// <param name="objectName">The name of the scope.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the next unique ID value.</returns>
        /// <remarks>
        /// Automatically initializes the scope if it doesn't exist by checking the database
        /// for the maximum value in the table with the same name and adding a buffer to that max ID.
        /// </remarks>
        public async Task<T> GetNextIdentityAsync<T>(string? objectName, CancellationToken cancellationToken = default) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return await GetNextIdentityInternalAsync<T>(objectName, cancellationToken);
        }

        private T GetNextIdentityInternal<T>(string? scopeName) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            if (_idScopes.TryGetValue(scopeName, out var idScope))
            {
                return ((IIdentityScope<T>)idScope).GetNextIdentity();
            }

            lock (_registrationlock)
            {
                if (_idScopes.TryGetValue(scopeName, out idScope))
                {
                    return ((IIdentityScope<T>)idScope).GetNextIdentity();
                }

                var newIdScope = _defaultScopeFactory.CreateIdentityScope<T>(scopeName);
                _idScopes[scopeName] = newIdScope;
                return newIdScope.GetNextIdentity();
            }
        }

        private async Task<T> GetNextIdentityInternalAsync<T>(string? scopeName, CancellationToken cancellationToken) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            if (_idScopes.TryGetValue(scopeName, out var idScope))
            {
                return await ((IIdentityScope<T>)idScope).GetNextIdentityAsync(cancellationToken);
            }

            using (await new AsyncLock(_registrationlock).LockAsync(cancellationToken))
            {
                if (_idScopes.TryGetValue(scopeName, out idScope))
                {
                    return await ((IIdentityScope<T>)idScope).GetNextIdentityAsync(cancellationToken);
                }

                var newIdScope = await _defaultScopeFactory.CreateIdentityScopeAsync<T>(scopeName, cancellationToken: cancellationToken);
                _idScopes[scopeName] = newIdScope;
                return await newIdScope.GetNextIdentityAsync(cancellationToken);
            }
        }
    }
}
