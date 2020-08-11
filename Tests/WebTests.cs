using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Web;
using Wivuu.GlobalCache;
using Xunit;

namespace Tests
{
    public class WebTests
        : IClassFixture<WebApplicationFactory<Web.Startup>>
    {
        public WebTests(WebApplicationFactory<Web.Startup> factory)
        {
            Factory = factory;
        }

        public WebApplicationFactory<Startup> Factory { get; }

        [Fact]
        public async Task TryGetCachedResponse()
        {
            // Clear cache
            var cache = Factory.Services.GetRequiredService<Wivuu.GlobalCache.IGlobalCache>();
            await cache.InvalidateAsync(CacheId.ForCategory("weather"));

            using var client = Factory.CreateClient();

            for (var tries = 0; tries < 2; ++tries)
            {
                var resp = await client.GetStringAsync("?days=10");
                var data = JsonConvert.DeserializeAnonymousType(resp, new [] {
                    new {
                        Date         = default(DateTimeOffset),
                        TemperatureC = default(decimal)
                    }
                });

                Assert.NotNull(data);
                Assert.Equal(10, data.Length);
                Assert.NotEqual(default, data[0].Date);
                Assert.NotEqual(default, data[0].TemperatureC);
            }
        }
    }
}