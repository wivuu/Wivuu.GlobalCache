using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface IGlobalCache
    {
        Task<T> GetOrCreateAsync<T>(CacheIdentity id,
                                    Func<Task<T>> generator,
                                    CancellationToken cancellationToken = default);

        Task InvalidateAsync(CacheIdentity id,
                             CancellationToken cancellationToken = default);

        Task CreateAsync<T>(CacheIdentity id,
                            Func<Task<T>> generator,
                            CancellationToken cancellationToken = default);

        Task<T> GetAsync<T>(CacheIdentity id,
                            CancellationToken cancellationToken = default);
    }
}
