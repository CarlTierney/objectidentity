using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectIdentity
{
    /// <summary>
    /// Defines a block or range of sequential identity values.
    /// </summary>
    /// <typeparam name="T">The type of IDs in this block (e.g., int, long).</typeparam>
    /// <remarks>
    /// <para>
    /// An identity block represents a contiguous range of identity values from Start to End.
    /// This interface is used to track and manage blocks of IDs that are retrieved from
    /// the underlying store and distributed to callers.
    /// </para>
    /// <para>
    /// For example, a block with Start=1001 and End=1100 represents 100 sequential IDs
    /// that can be assigned to new entities without requiring additional database calls.
    /// </para>
    /// </remarks>
    public interface IIdentityBlock<T> where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Gets or sets the starting value of this identity block.
        /// </summary>
        /// <remarks>
        /// This is the first ID value in the sequential range represented by this block.
        /// </remarks>
        T Start { get; set; }
        
        /// <summary>
        /// Gets or sets the ending value of this identity block.
        /// </summary>
        /// <remarks>
        /// This is the last ID value in the sequential range represented by this block.
        /// The number of IDs in the block is (End - Start + 1).
        /// </remarks>
        T End { get; set; }
    }
}
