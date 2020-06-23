using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class StorageTests
    {
        [Fact]
        public async Task TestGetOrCreateBlob()
        {
            var azStore = new BlobStorageProvider(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            var id     = new CacheIdentity("Test", 1);
            var str    = "hello world" + Guid.NewGuid();
            var writes = 0;

            await azStore.RemoveAsync(id);

            await Task.WhenAll(new []
            {
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
            });

            var otherTasks = new List<Task>();

            // Simulate bad things
            for (var i = 0; i < 500; ++i)
            {
                otherTasks.Add(
                    GetOrCreateAsync().ContinueWith(task =>
                        Assert.Equal(str, task.Result)
                    ));

                if (i % 5 == 0)
                    otherTasks.Add(azStore.RemoveAsync(id));
            }

            await Task.WhenAll(otherTasks);

            async Task<string> GetOrCreateAsync() => 
                await azStore.OpenReadWriteAsync(
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
