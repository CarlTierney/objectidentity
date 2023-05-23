using Pluralize.NET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.ObjectIdentity
{

    /// <summary>
    /// Identity manager is thread safe
    /// You only need one identity manager per database
    /// </summary>
    public class IdentityManager
    {
       
        
        private object _registrationlock = new object();
        private List<IIdentityScope> _idScopes = new List<IIdentityScope>();
        private IIdentityFactory _defaultScopeFactory;
        
       
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
        /// <exception cref="InvalidCastException"></exception>
        public void IntializeScope<T>(string scopeName, int startingId) where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            lock (_registrationlock)
            {
                var idScope = _idScopes.FirstOrDefault(a => a.Scope == scopeName && a.IdType == typeof(T)) as IIdentityScope<T>;

                if (idScope != null)
                {
                    throw new ArgumentException($"Identity scope {scopeName} already exists for type {typeof(T).Name}");
                }

                idScope = _defaultScopeFactory.CreateIdentityScope<T>(scopeName, startingId);
                _idScopes.Add(idScope);
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
        /// <exception cref="InvalidCastException"></exception>
        public void InitializeScoepe<TScope,T>(int startingId) where TScope : class
                                              where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {
            lock (_registrationlock)
            {
                var idScope = _idScopes.FirstOrDefault(a => a.Scope == typeof(TScope).Name && a.IdType == typeof(T)) as IIdentityScope<T>;

                if (idScope != null)
                {
                    throw new ArgumentException($"Identity scope {typeof(TScope).Name} already exists for type {typeof(T).Name}");
                }

                idScope = _defaultScopeFactory.CreateIdentityScope<T>(typeof(TScope).Name, startingId);
                _idScopes.Add(idScope);
            }
        }



        /// <summary>
        /// Automatically intializes the scope if a sequence for this object does not already exist by checking the database
        /// for the maximum value in the table with the same type name and adding a buffer to that max id 
        /// it will then generate a squence for that table with the same name as the type
        /// and grab ids for it
        /// </summary>
        /// <typeparam name="TScope"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetNextIdentity<TScope, T>() where TScope : class
                                              where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
        {

            var idScope = _idScopes.FirstOrDefault(a => a.Scope == typeof(TScope).Name && a.IdType == typeof(T)) as IIdentityScope<T>;

            if (idScope != null)
            {
                return idScope.GetNextIdentity();
            }

            lock (_registrationlock)
            {
                idScope = _idScopes.FirstOrDefault(a => a.Scope == typeof(TScope).Name && a.IdType == typeof(T)) as IIdentityScope<T>;

                if (idScope != null)
                {
                    return idScope.GetNextIdentity();
                }

                idScope = _defaultScopeFactory.CreateIdentityScope<T>(typeof(TScope).Name);
                _idScopes.Add(idScope);
                return idScope.GetNextIdentity();
            }

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

            var idScope = _idScopes.FirstOrDefault(a => a.Scope == objectName && a.IdType == typeof(T)) as IIdentityScope<T>;

            if (idScope != null)
            {
                return idScope.GetNextIdentity();
            }

            lock (_registrationlock)
            {
                idScope = _idScopes.FirstOrDefault(a => a.Scope == objectName && a.IdType == typeof(T)) as IIdentityScope<T>;

                if (idScope != null)
                {
                    return idScope.GetNextIdentity();
                }

                idScope = _defaultScopeFactory.CreateIdentityScope<T>(objectName);
                _idScopes.Add(idScope);
                return idScope.GetNextIdentity();
            }

        }


    }
}
