using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class CacheTests
    {
        [Theory]
        [InlineData(typeof(JsonSerializationProvider), typeof(BlobStorageProvider))]
        [InlineData(typeof(JsonSerializationProvider), typeof(FileStorageProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider), typeof(BlobStorageProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider), typeof(FileStorageProvider))]
        public async Task TestGeneralCaching(Type serializerType, Type storageProviderType)
        {
            IServiceProvider services;
            {
                var collection = new ServiceCollection();

                collection.AddWivuuGlobalCache(settings =>
                {
                    if (!(Activator.CreateInstance(serializerType) is ISerializationProvider serializer))
                        throw new Exception($"{serializerType} is not a serialization provider");

                    settings.SerializationProvider = serializer;
                        
                    switch (storageProviderType.Name)
                    {
                        case nameof(BlobStorageProvider):
                            var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                            var container         = blobServiceClient.GetBlobContainerClient("globalcache");

                            container.CreateIfNotExists();

                            settings.StorageProvider = new BlobStorageProvider(container);
                            break;

                        case nameof(FileStorageProvider):
                            settings.StorageProvider = new FileStorageProvider();
                            break;

                        default:
                            throw new NotSupportedException($"{nameof(storageProviderType)} is not supported");
                    }
                });

                services = collection.BuildServiceProvider();
            }

            using (var scope = services.CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<IGlobalCache>();
                
                // Remove item
                await cache.InvalidateAsync(CacheId.ForCategory("cachetest"));

                // Get or create item
                var item = await cache.GetOrCreateAsync(new CacheId("cachetest", "item5"), () =>
                {
                    return Task.FromResult(new { Item = 5 });
                });

                Assert.Equal(5, item.Item);
            }
        }

        [Theory]
        [InlineData(typeof(JsonSerializationProvider), typeof(BlobStorageProvider))]
        [InlineData(typeof(JsonSerializationProvider), typeof(FileStorageProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider), typeof(BlobStorageProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider), typeof(FileStorageProvider))]
        public async Task TestRawCaching(Type serializerType, Type storageProviderType)
        {
            IServiceProvider services;
            {
                var collection = new ServiceCollection();

                collection.AddWivuuGlobalCache(settings =>
                {
                    if (!(Activator.CreateInstance(serializerType) is ISerializationProvider serializer))
                        throw new Exception($"{serializerType} is not a serialization provider");

                    settings.SerializationProvider = serializer;
                        
                    switch (storageProviderType.Name)
                    {
                        case nameof(BlobStorageProvider):
                            var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                            var container         = blobServiceClient.GetBlobContainerClient("globalcache");

                            container.CreateIfNotExists();

                            settings.StorageProvider = new BlobStorageProvider(container);
                            break;

                        case nameof(FileStorageProvider):
                            settings.StorageProvider = new FileStorageProvider();
                            break;

                        default:
                            throw new NotSupportedException($"{nameof(storageProviderType)} is not supported");
                    }
                });

                services = collection.BuildServiceProvider();
            }

            using (var scope = services.CreateScope())
            {
                var cache = scope.ServiceProvider.GetRequiredService<IGlobalCache>();
                var writes = 0;
                
                // Remove item
                await cache.InvalidateAsync(CacheId.ForCategory("rawtest"));

                await TryWrite();
                await TryRead();

                await cache.InvalidateAsync(CacheId.ForCategory("rawtest"));

                await TryReadWrite();
                await TryReadWrite();
                await TryReadWrite();
                await TryRead();
                await TryRead();
                await TryRead();
                Assert.Equal(1, writes);
                
                async Task TryWrite()
                {
                    const int value = 6;

                    // Write item (raw)
                    await cache!.CreateRawAsync(new CacheId("rawtest", "item6"), async stream =>
                        await cache.SerializationProvider.SerializeToStreamAsync(new TestItem { Item = value }, stream)
                    );
                }
                

                async Task TryReadWrite()
                {
                    const int value = 6;

                    // Get item (raw)
                    using var resultStream = await cache!.GetOrCreateRawAsync(new CacheId("rawtest", "item6"), async stream =>
                    {
                        await cache.SerializationProvider.SerializeToStreamAsync(new TestItem { Item = value }, stream);
                        Interlocked.Increment(ref writes);
                    });

                    var item = await cache.SerializationProvider.DeserializeFromStreamAsync<TestItem>(resultStream);
                    Assert.NotNull(item);
                    Assert.Equal(value, item.Item);
                }
                
                async Task TryRead()
                {
                    const int value = 6;

                    // Get item (raw)
                    using var resultStream = await cache!.GetRawAsync(new CacheId("rawtest", "item6"));

                    var item = await cache.SerializationProvider.DeserializeFromStreamAsync<TestItem>(resultStream);
                    Assert.NotNull(item);
                    Assert.Equal(value, item.Item);
                }
            }
        }
    }
}