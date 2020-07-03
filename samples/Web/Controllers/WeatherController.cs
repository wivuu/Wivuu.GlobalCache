using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wivuu.GlobalCache;

namespace Web
{
    [Route("")]
    public class WeatherController : ControllerBase
    {
        [HttpGet]
        [GlobalCache("weather", DurationSecs = 30)]
        public async Task<IList<WeatherItem>> GetCachedAttrAsync(
            [FromServices]ILogger<WeatherController> logger,
            [FromServices]IGlobalCache cache)
        {
            const int days = 100;

            var items  = new List<WeatherItem>(capacity: days);
            var start  = DateTime.Today.AddDays(-days);
            var startc = 10;

            logger.LogWarning($"Retrieving items...");
            await Task.Yield();

            for (var i = 0; i < days; ++i, start = start.AddDays(1), startc += 1)
            {
                items.Add(new WeatherItem
                {
                    Date         = start,
                    TemperatureC = startc
                });
            }

            return items;
        }

        [HttpGet("cached")]
        [ProducesResponseType(typeof(IList<WeatherItem>), 200)]
        public async Task<System.IO.Stream> GetCachedAsync(
            [FromServices]ILogger<WeatherController> logger,
            [FromServices]IGlobalCache cache)
        {
            const int days = 100;

            var id = new CacheId("weather", 1);

            return await cache.GetOrCreateRawAsync(id, async stream =>
            {
                var items  = new List<WeatherItem>(capacity: days);
                var start  = DateTime.Today.AddDays(-days);
                var startc = 10;

                logger.LogWarning($"Retrieving items...");

                for (var i = 0; i < days; ++i, start = start.AddDays(1), startc += 1)
                {
                    items.Add(new WeatherItem
                    {
                        Date         = start,
                        TemperatureC = startc
                    });
                }

                await JsonSerializer.SerializeAsync(stream, items, new JsonSerializerOptions
                {
                    IgnoreNullValues     = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
            });
        }
        
        [HttpGet("clear")]
        public async Task<IActionResult> Clear(
            [FromServices] ILogger<WeatherController> logger,
            [FromServices] IGlobalCache cache)
        {
            logger.LogWarning("Clearing cache...");

            await cache.InvalidateAsync(CacheId.ForCategory("weather"));

            return Ok("Cleared cache");
        }
    }

    public class WeatherItem
    {
        public DateTimeOffset Date { get; set; }

        public int TemperatureC { get; set; }
    }
}