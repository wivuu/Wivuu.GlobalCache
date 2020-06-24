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

        public Task CreateAsync<T>(CacheIdentity id, Func<Task<T>> generator, CancellationToken cancellationToken = default) => 
            StorageProvider.OpenReadWriteAsync<T>(
                id,
                onWrite: async stream =>
                {
                    var data = await generator().ConfigureAwait(false);

                    // Write to stream
                    _ = SerializationProvider.SerializeToStreamAsync(data, stream, cancellationToken);

                    return data;
                },
                cancellationToken: cancellationToken);

        public Task<T> GetAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default) => 
            StorageProvider.OpenReadWriteAsync<T>(
                id,
                onRead: stream =>
                    // Read from stream
                    SerializationProvider.DeserializeFromStreamAsync<T>(stream, cancellationToken),
                cancellationToken: cancellationToken);

        public Task<T> GetOrCreateAsync<T>(CacheIdentity id, Func<Task<T>> generator, CancellationToken cancellationToken = default) =>
            StorageProvider.OpenReadWriteAsync<T>(
                id,
                onRead: stream =>
                    // Read from stream
                    SerializationProvider.DeserializeFromStreamAsync<T>(stream, cancellationToken),
                onWrite: async stream =>
                {
                    var data = await generator().ConfigureAwait(false);

                    // Write to stream
                    _ = SerializationProvider.SerializeToStreamAsync(data, stream, cancellationToken);

                    return data;
                },
                cancellationToken: cancellationToken);

        public Task InvalidateAsync(CacheIdentity id, CancellationToken cancellationToken = default) =>
            StorageProvider.RemoveAsync(id, cancellationToken);
    }
}