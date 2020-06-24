using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;
using Xunit.Abstractions;

namespace Tests
{
    public class StorageTests
    {
        public StorageTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        public ITestOutputHelper Output { get; set; }

        [Theory]
        [InlineData(typeof(BlobStorageProvider))]
        [InlineData(typeof(FileStorageProvider))]
        public async Task TestStorageConcurrence(Type storageProviderType)
        {
            IStorageProvider store;

            switch (storageProviderType.Name)
            {
                case nameof(BlobStorageProvider):
                    var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                    var container = blobServiceClient.GetBlobContainerClient("globalcache");
                    await container.CreateIfNotExistsAsync();
                
                    store = new BlobStorageProvider(container);
                    break;

                case nameof(FileStorageProvider):
                    store = new FileStorageProvider(new FileStorageSettings());
                    break;

                default:
                    throw new NotSupportedException($"{nameof(storageProviderType)} is not supported");
            }

            var id     = new CacheIdentity("concurrence", 1);
            var str    = "hello world" + Guid.NewGuid();
            var writes = 0;

            await store.RemoveAsync(id);

            await Task.WhenAll(new []
            {
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                store.RemoveAsync(id),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                store.RemoveAsync(id),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
            });

            // await store.RemoveAsync(id);

            async Task<string> GetOrCreateAsync() => 
                await store.OpenReadWriteAsync(
                    id,
                    onWrite: async stream =>
                    {
                        // !!!!Expensive data generation here!!!!
                        await stream.WriteAsync(Encoding.Default.GetBytes(str!));
                        Interlocked.Increment(ref writes);
                        // !!!!

                        return str!;
                    },
                    onRead: async stream =>
                    {
                        using var sr = new StreamReader(stream);

                        return await sr.ReadToEndAsync();
                    });
        }

        [Theory]
        [InlineData(typeof(BlobStorageProvider))]
        [InlineData(typeof(FileStorageProvider))]
        public async Task TestStorageRemove(Type storageProviderType)
        {
            IStorageProvider store;

            switch (storageProviderType.Name)
            {
                case nameof(BlobStorageProvider):
                    var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                    var container = blobServiceClient.GetBlobContainerClient("globalcache");
                    await container.CreateIfNotExistsAsync();

                    store = new BlobStorageProvider(container);
                    break;

                case nameof(FileStorageProvider):
                    store = new FileStorageProvider(new FileStorageSettings());
                    break;

                default:
                    throw new NotSupportedException($"{nameof(storageProviderType)} is not supported");
            }

            var id1 = new CacheIdentity("remove", 1);
            var id2 = new CacheIdentity("remove", 2);

            // Write two values
            var val1 = await store.OpenReadWriteAsync(id1, onWrite: stream => {
                stream.WriteByte(1);
                return Task.FromResult(1);
            });
            
            var val2 = await store.OpenReadWriteAsync(id2, onWrite: stream => {
                stream.WriteByte(2);
                return Task.FromResult(2);
            });

            // Clear 1 value
            Assert.True(await store.RemoveAsync(id1), "id1 should have been removed");
            await CheckRemoved(id1, 1);

            // Check 2 still exists
            Assert.Equal(val2, await store.OpenReadWriteAsync(id2, onRead: stream => Task.FromResult(stream.ReadByte())));

            // Clear ALL values
            Assert.True(await store.RemoveAsync(CacheIdentity.ForCategory("remove")), "id1 and id2 should have been removed");
            await CheckRemoved(id2, 2);

            async Task CheckRemoved(CacheIdentity id, byte notExpected)
            {
                try
                {
                    using var cts = new CancellationTokenSource();

                    cts.CancelAfter(TimeSpan.FromMilliseconds(20));

                    var value = await store.OpenReadWriteAsync(
                        id, 
                        onRead: stream => Task.FromResult(stream.ReadByte()),
                        cancellationToken: cts.Token
                    );

                    if (value == notExpected)
                        throw new Exception($"Item {id} should have been removed");
                }
                catch (TaskCanceledException)
                {
                    // Pass
                }
            }
        }
    }
}
