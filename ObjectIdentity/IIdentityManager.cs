using System;
using System.Threading;
using System.Threading.Tasks;

namespace ObjectIdentity
{
    /// <summary>
    /// Defines the contract for managing identity generation for different scopes in a thread-safe manner.
    /// </summary>
    /// <remarks>
    /// This interface allows for easy mocking and testing of identity generation in consuming applications.
    /// Only one instance is needed per database connection.
    /// </remarks>
    public interface IIdentityManager
    {
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
        void IntializeScope<T>(string? scopeName, int startingId) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;

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
        void InitializeScope<TScope, T>(int startingId) where TScope : class
                                                         where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;

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
        T GetNextIdentity<TScope, T>() where TScope : class
                                       where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;

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
        T GetNextIdentity<T>(string? objectName) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;

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
        Task<T> GetNextIdentityAsync<TScope, T>(CancellationToken cancellationToken = default) where TScope : class
                                                                                               where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;

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
        Task<T> GetNextIdentityAsync<T>(string? objectName, CancellationToken cancellationToken = default) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    }
}