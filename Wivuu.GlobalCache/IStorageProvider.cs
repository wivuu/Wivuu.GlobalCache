using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface IStorageProvider
    {
        Task<T> OpenReadWriteAsync<T>(CacheIdentity id, Func<Stream, Task<T>>? onRead, Func<Stream, Task<T>>? onWrite, CancellationToken cancellationToken = default);

        Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default);
    }
}