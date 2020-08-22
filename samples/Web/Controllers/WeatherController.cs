using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.Web;

namespace Web
{
    [Route("weather")]
    public class WeatherController : ControllerBase
    {
        [HttpGet("{country}")]
        [GlobalCache("weather/{country}/byday/{days}", DurationSecs=300, OffsetDurationSecs = -10)]
        public async Task<IList<WeatherItem>> GetCachedAttrAsync(
            [FromServices] ILogger<WeatherController> logger,
            [FromRoute] string country,
            [FromQuery] int days = 100)
        {
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

        [HttpGet("clear")]
        [GlobalCacheClear("weather/byday")]
        public IActionResult Clear(
            [FromServices] ILogger<WeatherController> logger,
            [FromServices] IGlobalCache cache) =>
            Ok("Cleared cache");
    }

    public class WeatherItem
    {
        public DateTimeOffset Date { get; set; }

        public int TemperatureC { get; set; }
    }
}