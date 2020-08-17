using System;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Wivuu.GlobalCache.Web
{
    public abstract class BaseCacheIdAttribute : Attribute
    {
        internal BaseCacheIdAttribute()
        {
        }

        /// <summary>
        /// Vary by request parameters, separated by semicolon. Use '*' for all request parameters.
        /// </summary>
        public string? VaryByParam { get; set; }

        /// <summary>
        /// Vary by request header, separated by semicolon
        /// </summary>
        public string? VaryByHeader { get; set; }

        /// <summary>
        /// Vary by custom logic, should be a type inheriting from `IGlobalCacheExpiration`
        /// </summary>
        public Type? VaryByCustom { get; set; }

        /// <summary>
        /// The cached item will be valid for at most this many seconds. Leave 
        /// unspecified or specify `0` for infinite duration
        /// </summary>
        public int DurationSecs { get; set; }

        /// <summary>
        /// By default `DurationSecs` will simply floor the period, for example: 60 seconds will always start at the
        /// start of every minute, and 3,600 will always default to the start of every hour. Using `OffsetDurationSecs`
        /// offsets that by the input seconds, so -600 on duration 3600 will start the interval at 10 minutes prior 
        /// to beginning of the hour.
        /// </summary>
        public int OffsetDurationSecs { get; set; }

        protected virtual int CalculateHashCode(ActionExecutingContext context)
        {
            unchecked
            {
                var result = 0;

                // Include duration in hashcode
                if (DurationSecs > 0)
                {
                    var floor = TimeSpan.FromSeconds(DurationSecs).Ticks;
                    var ticks = DateTimeOffset.UtcNow.Ticks / floor;
                    var date  = new DateTime(ticks * floor, DateTimeKind.Utc).AddSeconds(OffsetDurationSecs);

                    result = result ^ date.GetHashCode();
                }

                // Include params
                if (VaryByParam is string varyParam)
                {
                    if (varyParam == "*")
                    {
                        foreach (var p in context.ActionDescriptor.Parameters)
                        {
                            if (!p.BindingInfo.BindingSource.IsFromRequest)
                                continue;

                            if (context.ActionArguments.TryGetValue(p.Name, out var argValue))
                                result = result ^ CacheId.GetStringHashCode(p.Name) ^ (argValue?.GetHashCode() ?? 0);
                                
                            else if (context.RouteData.Values.TryGetValue(p.Name, out var routeValue))
                                result = result ^ CacheId.GetStringHashCode(p.Name) ^ (routeValue?.GetHashCode() ?? 0);
                        }
                    }
                    else
                    {
                        var parameters = VaryByParam.Split(';', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var arg in parameters)
                        {
                            if (context.ActionArguments.TryGetValue(arg, out var argValue))
                                result = result ^ CacheId.GetStringHashCode(arg) ^ (argValue?.GetHashCode() ?? 0);

                            else if (context.RouteData.Values.TryGetValue(arg, out var routeValue))
                                result = result ^ CacheId.GetStringHashCode(arg) ^ (routeValue?.GetHashCode() ?? 0);
                        }
                    }
                }

                // Include request headers
                if (VaryByHeader is string varyHeader)
                {
                    var reqHeaders = context.HttpContext.Request.Headers;
                    var parameters = varyHeader.Split(';', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var arg in parameters)
                    {
                        if (reqHeaders.TryGetValue(arg, out var value))
                            result = result ^ CacheId.GetStringHashCode(arg) ^ CacheId.GetStringHashCode(value);
                    }
                }

                // Vary by some custom input
                if (VaryByCustom != null && 
                    typeof(IGlobalCacheExpiration).IsAssignableFrom(VaryByCustom) && 
                    Activator.CreateInstance(VaryByCustom) is IGlobalCacheExpiration expr)
                    result = result ^ expr.GetId(context).GetHashCode();

                return result;
            }
        }

    }
}