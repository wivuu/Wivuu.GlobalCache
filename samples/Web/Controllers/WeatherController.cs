using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Web
{
    [Route("")]
    public class WeatherController : ControllerBase
    {
        [HttpGet]
        public async Task<IList<WeatherItem>> GetAsync()
        {
            await Task.Yield();

            const int days = 100;

            var items  = new List<WeatherItem>();
            var start  = DateTime.Today.AddDays(-days);
            var startc = 10;

            for (var i = 0; i < days; ++i, start = start.AddDays(1), startc += 1)
            {
                items.Add(new WeatherItem
                {
                    Date         = start,
                    TemperatureC = startc
                });

                await Task.Delay(25);
            }

            return items;
        }
    }

    public class WeatherItem
    {
        public DateTimeOffset Date { get; set; }

        public int TemperatureC { get; set; }
    }
}