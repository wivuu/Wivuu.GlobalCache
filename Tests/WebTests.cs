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
            const int count = 15;

            // Clear cache
            var cache = Factory.Services.GetRequiredService<Wivuu.GlobalCache.IGlobalCache>();
            await cache.InvalidateAsync(CacheId.ForCategory("weather"));

            using var client = Factory.CreateClient();

            for (var tries = 0; tries < 3; ++tries)
            {
                var resp = await client.GetAsync($"weather/us?days={count}");

                if (client.DefaultRequestHeaders.TryGetValues("If-None-Match", out _))
                {
                    // Expect the response to be empty and not modified
                    Assert.Equal(304, (int)resp.StatusCode);
                }
                else
                {
                    var respBody = await resp.Content.ReadAsStringAsync();

                    var data = JsonConvert.DeserializeAnonymousType(respBody, new [] {
                        new {
                            Date         = default(DateTimeOffset),
                            TemperatureC = default(decimal)
                        }
                    });

                    Assert.NotNull(data);
                    Assert.Equal(count, data.Length);
                    Assert.NotEqual(default, data[0].Date);
                    Assert.NotEqual(default, data[0].TemperatureC);

                    // Expect the response to be cached and return an etag
                }

                if (resp.Headers.TryGetValues("ETag", out var etags))
                {
                    client.DefaultRequestHeaders.Remove("If-None-Match");
                    client.DefaultRequestHeaders.Add("If-None-Match", etags);
                }
                else if (resp.TrailingHeaders.TryGetValues("ETag", out var trail))
                {
                    client.DefaultRequestHeaders.Remove("If-None-Match");
                    client.DefaultRequestHeaders.Add("If-None-Match", trail);
                }
            }
        }

        [Fact]
        public async Task TryClearCache()
        {
            const int count = 15;
            
            // Clear cache
            var cache = Factory.Services.GetRequiredService<Wivuu.GlobalCache.IGlobalCache>();
            await cache.InvalidateAsync(CacheId.ForCategory("weather"));

            using var client = Factory.CreateClient();

            for (var tries = 0; tries < 3; ++tries)
            {
                var resp = await client.GetAsync($"weather/us?days={count}");
                
                await client.GetAsync($"weather/clear/us");
            }
        }
    }
}