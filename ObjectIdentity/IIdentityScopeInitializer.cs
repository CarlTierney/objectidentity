using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectIdentity
{
    /// <summary>
    /// Defines the contract for initializing identity scopes with unique ID generation capabilities.
    /// </summary>
    /// <remarks>
    /// This interface is responsible for initializing a scope with an ID generation strategy, 
    /// and returning a function that can generate blocks of unique IDs.
    /// </remarks>
    public interface IIdentityScopeInitializer
    {
        /// <summary>
        /// Initializes an identity scope with the specified parameters and returns a function to generate ID blocks.
        /// </summary>
        /// <typeparam name="T">The type of IDs to generate (e.g., int, long).</typeparam>
        /// <param name="scope">The name of the scope to initialize.</param>
        /// <param name="startingId">Optional starting ID value for the scope.</param>
        /// <param name="maxValue">Optional maximum ID value allowed for this scope.</param>
        /// <returns>A function that takes a block size and returns a list of sequential IDs.</returns>
        Func<int, List<T>> Initialize<T>(string scope, long? startingId = null, long? maxValue = null)
            where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>;
    }

    /// <summary>
    /// Combines identity scope initialization with identity store operations.
    /// </summary>
    /// <remarks>
    /// This interface is intended for components that can both initialize identity scopes
    /// and provide direct access to the underlying identity storage system.
    /// </remarks>
    public interface IIdentityScopeStoreInitializer
    {
        
    }
}
