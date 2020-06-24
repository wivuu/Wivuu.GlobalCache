using System;
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
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.Serializer), typeof(BlobStorageProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.Serializer), typeof(FileStorageProvider))]
        public async Task TestGeneralCaching(Type serializerType, Type storageProviderType)
        {
            IServiceProvider services;
            {
                var collection = new ServiceCollection();

                collection.AddWivuuGlobalCache(settings =>
                {
                    if (!(Activator.CreateInstance(serializerType) is ISerializationProvider serializer))
                        throw new Exception($"{serializerType} is not a serialization provider");

                    settings.DefaultSerializationProvider = serializer;
                        
                    switch (storageProviderType.Name)
                    {
                        case nameof(BlobStorageProvider):
                            var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                            var container = blobServiceClient.GetBlobContainerClient("globalcache");
                            container.CreateIfNotExists();

                            settings.DefaultStorageProvider = new BlobStorageProvider(container);
                            break;

                        case nameof(FileStorageProvider):
                            settings.DefaultStorageProvider = new FileStorageProvider(new FileStorageSettings());
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
                await cache.InvalidateAsync(CacheIdentity.ForCategory("cachetest"));

                // Get or create item
                var item = await cache.GetOrCreateAsync(new CacheIdentity("cachetest", 0), () =>
                {
                    return Task.FromResult(new { Item = 5 });
                });

                Assert.Equal(5, item.Item);
            }
        }
    }
}