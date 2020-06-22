using System;
using System.Collections.Generic;
using System.IO;
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
        public async Task TestGetOrCreateBloc2()
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
                // Try to read the data
                await using (var handle = await azStore.GetReadWriteHandleAsync(id))
                {
                    if (handle.Writer is Stream writeStream)
                    {
                        // !!!!Expensive data generation here!!!!
                        await Task.Delay(1000);
                        await writeStream.WriteAsync(Encoding.Default.GetBytes(str!));
                        Interlocked.Increment(ref writes);
                        // !!!!

                        return str!;
                    }
                    else if (handle.Reader is Stream readStream)
                    {
                        using var sr = new StreamReader(readStream);
                        return await sr.ReadToEndAsync();
                    }
                }

                await azStore.WithLockAsync(id, async (client, lease) =>
                {
                    // Lease acquired
                });

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
    }
}
