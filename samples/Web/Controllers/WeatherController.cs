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
        [ProducesResponseType(typeof(IList<WeatherItem>), 200)]
        public async Task GetCachedAsync(
            [FromServices]ILogger<WeatherController> logger,
            [FromServices]IGlobalCache cache)
        {
            const int days = 100;

            var id = new CacheId("weather", 0);

            using var data = await cache.GetOrCreateRawAsync(id, async stream =>
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

                    await Task.Delay(5);
                }

                await JsonSerializer.SerializeAsync(stream, items, new JsonSerializerOptions
                {
                    IgnoreNullValues     = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
            });

            Response.Headers["Content-Type"] = "application/json";

            using var writer = Response.BodyWriter.AsStream();
            await data.CopyToAsync(writer, HttpContext.RequestAborted);
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