using Microsoft.Data.SqlClient;
using Pluralize.NET;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;


namespace Vision.ObjectIdentity
{
    public class IdentityScope<T> : IIdentityScope<T>
    {
        private Type _idType;
        private object _idLock = new object();
        
        private int _blockSize;
        private Queue<T> _availableIds;
        private string _scope;
        private Func<int,List<T>> _blockFunction;
        private bool _gettingNextBlock;
        private Task _activeBlockFunction;
        private object _nextBlockLock = new object();

        public IdentityScope(          
            int blockSize,
            string scope,
            Func<int,List<T>> blockFunction
            )
        {
            _idType = typeof(T);
            _scope = scope;
            _blockSize = blockSize;
            _blockFunction = blockFunction;
            _availableIds = new Queue<T>();
        }

       

        public Type IdType => _idType;

        public string Scope => _scope;

        

        

        public void CacheNextBlock()
        {
            throw new NotImplementedException();
        }

        public T GetNextIdentity()
        {
            lock(_idLock)
            {
                return GetNextId();                
            }
            
        }

        public void RecoverSkippedIds()
        {
            throw new NotImplementedException();
        }

       
        private T GetNextId()
        {
            lock(_idLock)
            {
                if(_availableIds.Count > _blockSize*.2)
                {
                    return _availableIds.Dequeue();
                }

                if(_availableIds.Count > 0)
                {
                    AddNextBlock();
                    return _availableIds.Dequeue();
                }

                if(_availableIds.Count == 0)
                {
                    
                    if(_gettingNextBlock && _activeBlockFunction != null)
                    {
                        _activeBlockFunction.Wait();
                        if (_availableIds.Count > 0)
                            return _availableIds.Dequeue();
                    }

                    AddNextBlock().Wait();
                    
                    return _availableIds.Dequeue();    
                    
                    
                    
                    

                                        
                }

                throw new Exception($"Unable to get next id for {_scope}");
            }
          
              
        }


        private Task AddNextBlock()
        {
            if(_gettingNextBlock)
            {
                return _activeBlockFunction;
            }

            if (_availableIds.Count > _blockSize * .2)
                return Task.CompletedTask;

            return GetNextBlock();

        }

        private Task GetNextBlock()
        {
           lock(_nextBlockLock)
            {

                _gettingNextBlock = true;
                _activeBlockFunction = Task.Run(() =>
                {
                    var result = _blockFunction(_blockSize);
                    foreach (var x in result)
                        _availableIds.Enqueue(x);
                }).ContinueWith((e) =>
                {
                    if(e.IsFaulted)
                    {
                        _gettingNextBlock = false;
                        throw e.Exception;
                    }
                    else
                    {
                        _gettingNextBlock = false;
                        _activeBlockFunction = null;
                    }
                });

                return _activeBlockFunction;
            }
        }

       

      
    }

   
}
