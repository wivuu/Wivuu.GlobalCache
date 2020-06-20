using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface IStorageProvider
    {
        Task<IAsyncDisposable> TryAcquireLockAsync(CacheIdentity id,
                                                   CancellationToken cancellationToken = default);

        Task RemoveAsync(CacheIdentity id,
                         CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync<T>(CacheIdentity id,
                                  CancellationToken cancellationToken = default);
        
        Stream OpenWrite<T>(CacheIdentity id,
                             CancellationToken cancellationToken = default);

        Task<Stream?> OpenReadAsync<T>(CacheIdentity id,
                                       CancellationToken cancellationToken = default);
    }
}