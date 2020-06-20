using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wivuu.GlobalCache;
using Xunit;

namespace Tests
{
    public class CacheTests
    {
        public CacheTests()
        {
            var collection = new ServiceCollection();

            Services = collection.BuildServiceProvider();
        }

        public ServiceProvider Services { get; }

        [Fact]
        public async Task TestGeneralCaching()
        {
            using var scope = Services.CreateScope();

            var cache = scope.ServiceProvider.GetRequiredService<IGlobalCache>();

            var item = await cache.GetOrCreateAsync(new CacheIdentity("Test"), e =>
            {
                return new ValueTask<int>(0);
            });

            Assert.Equal(0, item);
        }
    }
}