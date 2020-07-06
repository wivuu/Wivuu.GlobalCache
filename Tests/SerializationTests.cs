using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class SerializationTests
    {
        [Theory]
        [InlineData(typeof(JsonSerializationProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider))]
        public async Task TestSerializer(Type serializerType)
        {
            if (!(Activator.CreateInstance(serializerType) is ISerializationProvider serializer))
                throw new Exception($"{serializerType} is not a serialization provider");

            var data = new TestItem { Item = 5, StrSomething = "Hello!" };
            using var ms = new MemoryStream();

            await serializer.SerializeToStreamAsync(data, ms);
            ms.Position = 0; // Rewind stream

            var resultData = await serializer.DeserializeFromStreamAsync<TestItem>(ms);

            Assert.NotNull(resultData);
            Assert.Equal(data.Item, resultData.Item);
            Assert.Equal(data.StrSomething, resultData.StrSomething);
        }

        [Theory]
        [InlineData(typeof(JsonSerializationProvider), typeof(BlobStorageProvider))]
        [InlineData(typeof(JsonSerializationProvider), typeof(FileStorageProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider), typeof(BlobStorageProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider), typeof(FileStorageProvider))]
        public async Task TestStoreSerializers(Type serializerType, Type storageProviderType)
        {
            IStorageProvider store;

            if (!(Activator.CreateInstance(serializerType) is ISerializationProvider serializer))
                throw new Exception($"{serializerType} is not a serialization provider");
                
            switch (storageProviderType.Name)
            {
                case nameof(BlobStorageProvider):
                    var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                    var container = blobServiceClient.GetBlobContainerClient("globalcache");
                    await container.CreateIfNotExistsAsync();

                    store = new BlobStorageProvider(container);
                    break;

                case nameof(FileStorageProvider):
                    store = new FileStorageProvider();
                    break;

                default:
                    throw new NotSupportedException($"{nameof(storageProviderType)} is not supported");
            }

            var id = new CacheId(serializerType.Name, 5);
            await store.RemoveAsync(id);

            // Write data
            var written = await OpenReadWriteAsync(id, onWrite: async stream =>
            {
                // Open database and query data
                var items = new List<string>();
                
                await foreach (var item in GetData())
                    items.Add(item);

                await serializer.SerializeToStreamAsync(items, stream);

                return items;
            });

            Assert.NotNull(written);
            Assert.NotEmpty(written);

            // Read data
            var read = await OpenReadWriteAsync(id, onRead: async stream =>
                await serializer.DeserializeFromStreamAsync<List<string>>(stream)
            );

            Assert.NotNull(read);
            Assert.Equal(written.Count, read.Count);

            for (var i = 0; i < written.Count; ++i)
                Assert.Equal(written[i], read[i]);

            async IAsyncEnumerable<string> GetData()
            {
                for (var i = 0; i < 1000; ++i)
                {
                    await Task.Yield();
                    yield return $"Item {i}";
                }
            }

            async Task<T> OpenReadWriteAsync<T>(CacheId id,
                                                Func<Stream, Task<T>>? onWrite = null,
                                                Func<Stream, Task<T>>? onRead = null, 
                                                CancellationToken cancellationToken = default)
            {
                var retries = new RetryHelper(1, 500, totalMaxDelay: TimeSpan.FromSeconds(10));

                // Wait for a break in traffic
                do
                {
                    // Try to READ the file
                    if (onRead != null && await store.TryOpenRead(id, cancellationToken) is Stream reader)
                    {
                        using (reader)
                            return await onRead(reader);
                    }

                    // Try to WRITE file
                    if (onWrite != null && await store.TryOpenWrite(id, cancellationToken) is StreamWithCompletion writer)
                    {
                        try
                        {
                            using (writer)
                                return await onWrite(writer);
                        }
                        finally
                        {
                            await writer;
                        }
                    }

                    if (await retries.DelayAsync(cancellationToken).ConfigureAwait(false) == false)
                        throw new TimeoutException();
                }
                while (!cancellationToken.IsCancellationRequested);

                throw new TaskCanceledException();
            }
        }
    }

    public class TestItem
    {
        public int Item { get; set; }
        public string StrSomething { get; set; } = "This is a test";
    }
}
