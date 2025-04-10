using Pluralize.NET;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Vision.ObjectIdentity
{
    /// <summary>
    /// Identity manager is thread safe
    /// You only need one identity manager per database
    /// </summary>
    public class IdentityManager
    {
        private readonly object _registrationlock = new object();
        private readonly ConcurrentDictionary<string, IIdentityScope> _idScopes = new ConcurrentDictionary<string, IIdentityScope>();
        private readonly IIdentityFactory _defaultScopeFactory;

        public IdentityManager(IIdentityFactory scopeFactory)
        {
            _defaultScopeFactory = scopeFactory;
        }

        /// <summary>
        /// Only use this when you specifically have to set the initial starting id
        /// the identity factory will attempt to check the table for the name you provided and will automatically get its max value without you having to specify it
        /// will throw an argument exception if the scope already exists
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="scopeName"></param>
        /// <param name="startingId"></param>
        /// <exception cref="ArgumentException"></exception>
        public void IntializeScope<T>(string scopeName, int startingId) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
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
        /// Only use this when you specifically have to set the initial starting id
        /// the identity factory will attempt to check the table for the name you provided and will automatically get its max value without you having to specify it
        /// will throw an argument exception if the scope already exists
        /// </summary>
        /// <typeparam name="TScope"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="startingId"></param>
        /// <exception cref="ArgumentException"></exception>
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
        /// Automatically initializes the scope if a sequence for this object does not already exist by checking the database
        /// for the maximum value in the table with the same type name and adding a buffer to that max id 
        /// it will then generate a sequence for that table with the same name as the type
        /// and grab ids for it
        /// </summary>
        /// <typeparam name="TScope"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetNextIdentity<TScope, T>() where TScope : class
                                              where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            var scopeName = typeof(TScope).Name;
            return GetNextIdentityInternal<T>(scopeName);
        }

        /// <summary>
        /// Automatically gets the next identity for the object name provided
        /// will attempt to read a table for the same name if it exists during initialization and buffer its max id 
        /// then provide you with the next id after the max + buffer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectName"></param>
        /// <returns></returns>
        public T GetNextIdentity<T>(string objectName) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            return GetNextIdentityInternal<T>(objectName);
        }

        private T GetNextIdentityInternal<T>(string scopeName) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
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
    }
}
