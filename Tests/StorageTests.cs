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
        public async Task TestAzureReadWrite()
        {
            var azStore = new BlobStorageProvider(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            var id  = new CacheIdentity("Test", 1);
            var str = "hello world" + Guid.NewGuid();

            await azStore.RemoveAsync(id);

            // Write file
            using (var stream = await azStore.OpenWriteAsync(id))
            {
                var data = Encoding.Default.GetBytes(str);

                await stream.WriteAsync(data);
            }

            // Read file
            using (var stream = azStore.OpenReadAsync(id))
            {
                using var sr = new StreamReader(stream);
                var data     = await sr.ReadToEndAsync();

                Assert.Equal(str, data);
            }
        }
        
        [Fact]
        public async Task TestGetOrCreateBlock()
        {
            var azStore = new BlobStorageProvider(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            var id     = new CacheIdentity("Test", 2);
            var str    = "hello world" + Guid.NewGuid();
            var writes = 0;

            await azStore.RemoveAsync(id);

            await Task.WhenAll(new []
            {
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
                GetOrCreateAsync().ContinueWith(task => Assert.Equal(str, task.Result)),
            });

            var otherTasks = new List<Task>();

            // Simulate bad things
            for (var i = 0; i < 100; ++i)
            {
                otherTasks.Add(Task.Run(async () => Assert.Equal(str, await GetOrCreateAsync())));

                // if (i % 5 == 0)
                // {
                //     await azStore.WaitForLock(id);
                //     await azStore.RemoveAsync(id);
                // }
            }

            await Task.WhenAll(otherTasks);

            // Assert.Equal(1, writes);

            async Task<string> GetOrCreateAsync()
            {
                // Attempt to read data
                switch (await azStore!.ExistsAsync(id))
                {
                    case CacheStatus.NotExists:
                        while (true)
                        {
                            try
                            {
                                // Attempt write data
                                using var writer = await azStore.OpenWriteAsync(id);

                                // !!!!Expensive data generation here!!!!
                                {
                                    await Task.Delay(1000);
                                    await writer.WriteAsync(Encoding.Default.GetBytes(str!));

                                    Interlocked.Increment(ref writes);

                                    return str!;
                                }
                            }
                            catch (RequestFailedException e)
                            {
                                if (e.Status == 409) // Blob already exists
                                {
                                    if (await azStore.WaitForLock(id))
                                        return await GetAsync();
                                    else
                                        continue;
                                }

                                throw;
                            }   
                        }
                        
                    case CacheStatus.Locked:
                        await azStore.WaitForLock(id);
                        return await GetAsync();

                    default:
                        return await GetAsync();
                }
            }

            async Task<string> GetAsync()
            {
                await using (var stream = azStore!.OpenReadAsync(id))
                {
                    using var sr = new StreamReader(stream);
                    var result = await sr.ReadToEndAsync();
                    return result;
                }
            }
        }
        
        [Fact]
        public async Task TestGetOrCreateBlob2()
        {
            var azStore = new BlobStorageProvider2(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            var id     = new CacheIdentity("Test", 2);
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

            async Task<string> GetOrCreateAsync()
            {
                // Try to read the data
                await using (var handle = await azStore.CreateReaderWriterHandle(id))
                {
                    if (handle.Writer is PipeWriter writer)
                    {
                        // !!!!Expensive data generation here!!!!
                        await writer.WriteAsync(Encoding.Default.GetBytes(str!));
                        Interlocked.Increment(ref writes);
                        writer.Complete();
                        // !!!!

                        return str!;
                    }
                    else if (handle.Reader is PipeReader reader)
                    {
                        using var readStream = reader.AsStream();
                        using var sr = new StreamReader(readStream);

                        return await sr.ReadToEndAsync();
                    }
                    else
                        throw new NotSupportedException();
                }
            }
        }
        
        [Fact]
        public async Task TestGetOrCreateBlob3()
        {
            var azStore = new BlobStorageProvider3(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            var id     = new CacheIdentity("Test", 3);
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

            async Task<string> GetOrCreateAsync()
            {
                // Try to read the data
                var handle = new ReaderWriterHandle<string>()
                    .OnWrite(async stream =>
                    {
                        // !!!!Expensive data generation here!!!!
                        await stream.WriteAsync(Encoding.Default.GetBytes(str!));
                        Interlocked.Increment(ref writes);
                        // !!!!

                        return str!;
                    })
                    .OnRead(async stream =>
                    {
                        using var sr = new StreamReader(stream);

                        return await sr.ReadToEndAsync();
                    });

                return await azStore.OpenReadWriteAsync(id, handle);
            }
        }
    }
}
