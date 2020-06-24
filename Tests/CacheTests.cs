using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class CacheTests
    {
        public CacheTests()
        {
            var collection = new ServiceCollection();

            collection.AddSingleton<IGlobalCache>(service =>
            {
                return new DefaultGlobalCache(new GlobalCacheSettings());
            });

            // collection.AddScoped<IStorageProvider>(services =>
            // {
            //     return new BlobStorageProvider(new StorageSettings
            //     {
            //         ConnectionString = "UseDevelopmentStorage=true"
            //     });
            // });

            Services = collection.BuildServiceProvider();
        }

        public ServiceProvider Services { get; }

        [Fact]
        public async Task TestGeneralCaching()
        {
            using var scope = Services.CreateScope();

            var cache = scope.ServiceProvider.GetRequiredService<IGlobalCache>();

            var item = await cache.GetOrCreateAsync(new CacheIdentity("Test", 0), () =>
            {
                return Task.FromResult(0);
            });

            Assert.Equal(0, item);
        }
    }
}