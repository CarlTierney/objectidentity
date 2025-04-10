using Microsoft.Data.SqlClient;
using Pluralize.NET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Vision.ObjectIdentity
{
    public class IdentityScope<T> : IIdentityScope<T> where T : struct, IComparable, IConvertible, IFormattable, IComparable<T>, IEquatable<T>
    {
        private readonly Type _idType;
        private readonly object _idLock = new object();
        private readonly int _blockSize;
        private readonly ConcurrentQueue<T> _availableIds;
        private readonly string _scope;
        private readonly Func<int, List<T>> _blockFunction;
        private bool _gettingNextBlock;
        private Task _activeBlockFunction;
        private readonly object _nextBlockLock = new object();

        public IdentityScope(
            int blockSize,
            string scope,
            Func<int, List<T>> blockFunction
            )
        {
            _idType = typeof(T);
            _scope = scope;
            _blockSize = blockSize;
            _blockFunction = blockFunction;
            _availableIds = new ConcurrentQueue<T>();
        }

        public Type IdType => _idType;

        public string Scope => _scope;

        public void CacheNextBlock()
        {
            // Not implemented
        }

        public T GetNextIdentity()
        {
            lock (_idLock)
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
            lock (_idLock)
            {
                if (_availableIds.Count > _blockSize * 0.2)
                {
                    if (_availableIds.TryDequeue(out var id))
                    {
                        return id;
                    }
                }

                if (_availableIds.Count > 0)
                {
                    AddNextBlock();
                    if (_availableIds.TryDequeue(out var id))
                    {
                        return id;
                    }
                }

                if (_availableIds.Count == 0)
                {
                    if (_gettingNextBlock && _activeBlockFunction != null)
                    {
                        _activeBlockFunction.Wait();
                        if (_availableIds.TryDequeue(out var nid))
                        {
                            return nid;
                        }
                    }

                    AddNextBlock().Wait();
                    if (_availableIds.TryDequeue(out var id))
                    {
                        return id;
                    }
                }

                throw new InvalidOperationException($"Unable to get next id for {_scope}");
            }
        }

        private Task AddNextBlock()
        {
            if (_gettingNextBlock)
            {
                return _activeBlockFunction;
            }

            if (_availableIds.Count > _blockSize * 0.2)
            {
                return Task.CompletedTask;
            }

            return GetNextBlock();
        }

        private Task GetNextBlock()
        {
            lock (_nextBlockLock)
            {
                _gettingNextBlock = true;
                _activeBlockFunction = Task.Run(() =>
                {
                    var result = _blockFunction(_blockSize);
                    foreach (var x in result)
                    {
                        _availableIds.Enqueue(x);
                    }
                }).ContinueWith((e) =>
                {
                    if (e.IsFaulted)
                    {
                        _gettingNextBlock = false;
                        throw new InvalidOperationException("Failed to get the next block of IDs.", e.Exception);
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
