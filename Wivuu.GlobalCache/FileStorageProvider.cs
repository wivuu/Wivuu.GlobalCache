using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public class FileStorageProvider : IStorageProvider
    {
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

        public Stream OpenWrite<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}