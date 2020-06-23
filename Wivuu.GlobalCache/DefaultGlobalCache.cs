using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public class DefaultGlobalCache : IGlobalCache
    {
        public DefaultGlobalCache(GlobalCacheSettings settings)
        {
            Settings              = settings;
            SerializationProvider = settings.DefaultSerializationProvider ?? new JsonSerializationProvider();
            StorageProvider       = settings.DefaultStorageProvider ?? new FileStorageProvider(new FileStorageSettings());
        }

        public GlobalCacheSettings Settings { get; }
        public ISerializationProvider SerializationProvider { get; }
        public IStorageProvider StorageProvider { get; }

        public Task CreateAsync<T>(CacheIdentity id, Func<GlobalCacheEntrySettings, Task<T>> generator, CancellationToken cancellationToken = default)
        {
            StorageProvider.OpenReadWriteAsync(id, onWrite: stream =>
            {
                return generator();
            });
        }

        public ValueTask<T> GetAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<T> GetOrCreateAsync<T>(CacheIdentity id, Func<GlobalCacheEntrySettings, ValueTask<T>> generator, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task InvalidateAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}