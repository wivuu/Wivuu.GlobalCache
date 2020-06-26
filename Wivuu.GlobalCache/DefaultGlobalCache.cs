using System;
using System.IO;
using System.IO.Pipelines;
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

        public ISerializationProvider SerializationProvider { get; }

        public IStorageProvider StorageProvider { get; }

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

        public async Task CreateRawAsync(CacheId id, Func<Stream, Task> generator, CancellationToken cancellationToken = default)
        {
            await StorageProvider.OpenReadWriteAsync<int>(
                id,
                onWrite: async storageStream =>
                {
                    // Have generator write to a new pipe
                    var innerPipe = new Pipe();

                    _ = Task.Run(async () =>
                    {
                        using var outputStream = innerPipe.Writer.AsStream();
                        await generator(outputStream).ConfigureAwait(false);
                    });

                    // Copy inner pipe to outer pipe and storage stream simultaneously
                    using var readStream = innerPipe.Reader.AsStream();
                    await innerPipe.Reader.CopyToAsync(storageStream, cancellationToken).ConfigureAwait(false);
                    return 0;
                },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }

        public Task<Stream> GetRawAsync(CacheId id, CancellationToken cancellationToken = default)
        {
            var outerPipe = new Pipe();

            _ = Task.Run(() =>
                StorageProvider.OpenReadWriteAsync<int>(
                    id,
                    onRead: async storageStream =>
                    {
                        using var outputStream = outerPipe.Writer.AsStream(true);

                        try
                        {
                            await storageStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                            outerPipe.Writer.Complete();
                        }
                        catch (Exception error)
                        {
                            outerPipe.Writer.Complete(error);
                        }

                        return 0;
                    },
                    cancellationToken: cancellationToken)
            );

            return Task.FromResult(outerPipe.Reader.AsStream());
        }

        public Task<Stream> GetOrCreateRawAsync(CacheId id, Func<Stream, Task> generator, CancellationToken cancellationToken = default)
        {
            var outerPipe = new Pipe();

            _ = Task.Run(() =>
                StorageProvider.OpenReadWriteAsync<int>(
                    id,
                    onRead: async storageStream => {
                        using var outputStream = outerPipe.Writer.AsStream(true);
                        await storageStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                        outerPipe.Writer.Complete();
                        return 0;
                    },
                    onWrite: async storageStream =>
                    {
                        // Have generator write to a new pipe
                        var innerPipe = new Pipe();

                        _ = Task.Run(async () =>
                        {
                            using var outputStream = innerPipe.Writer.AsStream();
                            await generator(outputStream).ConfigureAwait(false);
                        });

                        // Copy inner pipe to outer pipe and storage stream simultaneously
                        var reader = innerPipe.Reader;
                        var writer = outerPipe.Writer;

                        while (true)
                        {
                            var result   = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                            var buffer   = result.Buffer;
                            var position = buffer.Start;
                            var consumed = position;

                            try
                            {
                                while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory))
                                {
                                    await writer.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
                                    await storageStream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);

                                    consumed = position;
                                }

                                // The while loop completed succesfully, so we've consumed the entire buffer.
                                consumed = buffer.End;

                                if (result.IsCompleted)
                                    break;
                            }
                            finally
                            {
                                reader.AdvanceTo(consumed);
                            }
                        }

                        writer.Complete();
                        reader.Complete();

                        return 0;
                    },
                    cancellationToken: cancellationToken
                )
            );

            return Task.FromResult(outerPipe.Reader.AsStream());
        }

        public Task InvalidateAsync(CacheId id, CancellationToken cancellationToken = default) =>
            StorageProvider.RemoveAsync(id, cancellationToken);
    }
}