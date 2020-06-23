using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class StorageTests
    {
        [Theory]
        [InlineData(typeof(BlobStorageProvider))]
        [InlineData(typeof(FileStorageProvider))]
        public async Task TestStorageConcurrence(Type storageProviderType)
        {
            IStorageProvider store;

            switch (storageProviderType.Name)
            {
                case nameof(BlobStorageProvider):
                    var azStore = new BlobStorageProvider(new StorageSettings
                    {
                        ConnectionString = "UseDevelopmentStorage=true"
                    });
                    
                    await azStore.EnsureContainerAsync();

                    store = azStore;
                    break;

                case nameof(FileStorageProvider):
                    store = new FileStorageProvider(new FileStorageSettings());
                    break;

                default:
                    throw new NotSupportedException($"{nameof(storageProviderType)} is not supported");
            }

            var id     = new CacheIdentity("Test", 1);
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
    }
}
