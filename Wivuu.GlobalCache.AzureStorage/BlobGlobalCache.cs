using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache.AzureStorage
{
    public class BlobGlobalCache : IGlobalCache
    {
        public Task CreateAsync<T>(CacheIdentity id, Func<GlobalCacheEntrySettings, Task<T>> generator, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task CreateAsync<T>(CacheIdentity id, Func<GlobalCacheEntrySettings, IAsyncEnumerable<T>> generator, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<T> GetAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<T> GetManyAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<T> GetOrCreateAsync<T>(CacheIdentity id, Func<GlobalCacheEntrySettings, ValueTask<T>> generator, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<T> GetOrCreateAsync<T>(CacheIdentity id, Func<GlobalCacheEntrySettings, IAsyncEnumerable<T>> generator, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task InvalidateAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
