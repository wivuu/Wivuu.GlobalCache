using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Wivuu.GlobalCache
{
    public class DefaultGlobalCache : IGlobalCache
    {
        public DefaultGlobalCache(IOptions<GlobalCacheSettings> settings)
        {
            Settings              = settings.Value;
            SerializationProvider = Settings.SerializationProvider ?? new JsonSerializationProvider();
            StorageProvider       = Settings.StorageProvider ?? new FileStorageProvider();
        }

        protected GlobalCacheSettings Settings { get; }
        
        protected ISerializationProvider SerializationProvider { get; }
        
        protected IStorageProvider StorageProvider { get; }

        public Task CreateAsync<T>(CacheId id, Func<Task<T>> generator, CancellationToken cancellationToken = default) => 
            StorageProvider.OpenReadWriteAsync<T>(
                id,
                onWrite: async stream =>
                {
                    var data = await generator().ConfigureAwait(false);

                    // Write to stream
                    await SerializationProvider.SerializeToStreamAsync(data, stream, cancellationToken);

                    return data;
                },
                cancellationToken: cancellationToken);

        public Task<T> GetAsync<T>(CacheId id, CancellationToken cancellationToken = default) => 
            StorageProvider.OpenReadWriteAsync<T>(
                id,
                onRead: stream =>
                    // Read from stream
                    SerializationProvider.DeserializeFromStreamAsync<T>(stream, cancellationToken),
                cancellationToken: cancellationToken);

        public Task<T> GetOrCreateAsync<T>(CacheId id, Func<Task<T>> generator, CancellationToken cancellationToken = default) =>
            StorageProvider.OpenReadWriteAsync<T>(
                id,
                onRead: stream =>
                    // Read from stream
                    SerializationProvider.DeserializeFromStreamAsync<T>(stream, cancellationToken),
                onWrite: async stream =>
                {
                    var data = await generator().ConfigureAwait(false);

                    // Write to stream
                    await SerializationProvider.SerializeToStreamAsync(data, stream, cancellationToken);

                    return data;
                },
                cancellationToken: cancellationToken);

        public Task InvalidateAsync(CacheId id, CancellationToken cancellationToken = default) =>
            StorageProvider.RemoveAsync(id, cancellationToken);
    }
}