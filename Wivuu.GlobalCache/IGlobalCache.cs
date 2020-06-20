﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface IGlobalCache
    {
        ValueTask<T> GetOrCreateAsync<T>(CacheIdentity id,
                                         Func<GlobalCacheEntrySettings, ValueTask<T>> generator,
                                         CancellationToken cancellationToken = default);

        IAsyncEnumerable<T> GetOrCreateAsync<T>(CacheIdentity id,
                                                Func<GlobalCacheEntrySettings, IAsyncEnumerable<T>> generator,
                                                CancellationToken cancellationToken = default);

        Task InvalidateAsync(CacheIdentity id,
                             CancellationToken cancellationToken = default);

        Task CreateAsync<T>(CacheIdentity id,
                            Func<GlobalCacheEntrySettings, Task<T>> generator,
                            CancellationToken cancellationToken = default);

        Task CreateAsync<T>(CacheIdentity id,
                            Func<GlobalCacheEntrySettings, IAsyncEnumerable<T>> generator,
                            CancellationToken cancellationToken = default);

        ValueTask<T> GetAsync<T>(CacheIdentity id,
                                 CancellationToken cancellationToken = default);
                            
        IAsyncEnumerable<T> GetManyAsync<T>(CacheIdentity id,
                                            CancellationToken cancellationToken = default);
    }
}
