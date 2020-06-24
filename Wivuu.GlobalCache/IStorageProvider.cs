using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface IStorageProvider
    {
        Task<T> OpenReadWriteAsync<T>(CacheIdentity id,
                                      Func<Stream, Task<T>>? onRead = default,
                                      Func<Stream, Task<T>>? onWrite = default,
                                      CancellationToken cancellationToken = default);

        Task<bool> RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default);
    }
}