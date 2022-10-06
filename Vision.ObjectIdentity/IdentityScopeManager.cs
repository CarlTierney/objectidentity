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

            var idScope = _idScopes.FirstOrDefault(a => a.ForType == typeof(TScope) && a.IdType == typeof(T)) as IIdentityScope<TScope, T>;

            if (idScope != null)
            {
                return idScope.GetNextIdentity();
            }

            lock (_registrationlock)
            {
                idScope = _idScopes.FirstOrDefault(a => a.ForType == typeof(TScope) && a.IdType == typeof(T)) as IIdentityScope<TScope, T>;

                if (idScope != null)
                {
                    return idScope.GetNextIdentity();
                }

                idScope = _defaultScopeFactory.CreateIdentityScope<TScope, T>();
                _idScopes.Add(idScope);
                return idScope.GetNextIdentity();
            }

        }

        
    }
}
