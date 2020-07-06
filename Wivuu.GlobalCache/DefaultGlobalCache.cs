using System;
using System.IO;
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
            Timeout               = Settings.RetryTimeout ?? TimeSpan.FromSeconds(60);
        }

        protected GlobalCacheSettings Settings { get; }

        public ISerializationProvider SerializationProvider { get; }

        public IStorageProvider StorageProvider { get; }

        public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(60);

        public async Task CreateAsync<T>(CacheId id, Func<Task<T>> generator, CancellationToken cancellationToken = default)
        {
            var retries = new RetryHelper(1, 500, totalMaxDelay: Timeout);

            do
            {
                if (await StorageProvider.TryOpenWrite(id, cancellationToken) is StreamWithCompletion stream)
                {
                    var data = await generator().ConfigureAwait(false);

                    // Write to stream
                    using (stream)
                        await SerializationProvider.SerializeToStreamAsync(data, stream, cancellationToken);

                    await stream;
                }
                
                if (await retries.DelayAsync(cancellationToken).ConfigureAwait(false) == false)
                    throw new TimeoutException();
            }
            while (!cancellationToken.IsCancellationRequested);

            throw new TaskCanceledException();
        }

        public async Task<T> GetAsync<T>(CacheId id, CancellationToken cancellationToken = default)
        {
            var retries = new RetryHelper(1, 500, totalMaxDelay: Timeout);

            do
            {
                if (await StorageProvider.TryOpenRead(id, cancellationToken) is Stream stream)
                {
                    // Read from stream
                    using (stream)
                        return await SerializationProvider.DeserializeFromStreamAsync<T>(stream, cancellationToken);
                }

                if (await retries.DelayAsync(cancellationToken).ConfigureAwait(false) == false)
                    throw new TimeoutException();
            }
            while (!cancellationToken.IsCancellationRequested);

            throw new TaskCanceledException();
        }

        public async Task<T> GetOrCreateAsync<T>(CacheId id, Func<Task<T>> generator, CancellationToken cancellationToken = default)
        {
            var retries = new RetryHelper(1, 500, totalMaxDelay: Timeout);

            do
            {
                if (await StorageProvider.TryOpenRead(id, cancellationToken) is Stream readStream)
                {
                    // Read from stream
                    using (readStream)
                        return await SerializationProvider.DeserializeFromStreamAsync<T>(readStream, cancellationToken);
                }
                else if (await StorageProvider.TryOpenWrite(id, cancellationToken) is StreamWithCompletion writeStream)
                {
                    var data = await generator().ConfigureAwait(false);

                    // Write to stream
                    using (writeStream)
                        await SerializationProvider.SerializeToStreamAsync(data, writeStream, cancellationToken);

                    await writeStream;

                    return data;
                }

                if (await retries.DelayAsync(cancellationToken).ConfigureAwait(false) == false)
                    throw new TimeoutException();
            }
            while (!cancellationToken.IsCancellationRequested);

            throw new TaskCanceledException();
        }

        public Task InvalidateAsync(CacheId id, CancellationToken cancellationToken = default) =>
            StorageProvider.RemoveAsync(id, cancellationToken);
    }
}