using Pluralize.NET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.ObjectIdentity
{
    public class IdentityManager
    {
       
        
        private object _registrationlock = new object();
        private List<IIdentityScope> _idScopes = new List<IIdentityScope>();
        private IIdentityFactory _defaultScopeFactory;
        
       
        public IdentityManager(IIdentityFactory scopeFactory)
        {
            _defaultScopeFactory = scopeFactory;
        }


     

        public  T GetNextIdentity<TScope,T>() where TScope: class
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

        public T GetNextIdentity<T>(string objectName)
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
