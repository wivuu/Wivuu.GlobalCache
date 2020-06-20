using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache.Azure
{
    public class BlobStorageProvider : IStorageProvider
    {
        public BlobStorageProvider(GlobalCacheSettings settings)
        {
            this.Settings = settings;
        }

        public GlobalCacheSettings Settings { get; }

        public Task<bool> ExistsAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> OpenReadAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncDisposable> TryAcquireLockAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> WriteAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}