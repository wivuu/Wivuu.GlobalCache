using System;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;
using static Wivuu.GlobalCache.CacheId;

namespace Wivuu.GlobalCache.Web
{
    public abstract class BaseCacheIdAttribute : Attribute
    {
        internal BaseCacheIdAttribute(string category)
        {
            this.Category = category;
        }

        /// <summary>
        /// The a root category of the cached items (including route parameter strings)
        /// </summary>
        public string Category { get; }

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

        /// <summary>
        /// Replace any parameterized route variables
        /// </summary>
        protected static string CalculateCategory(ActionExecutingContext context, string category)
        {
            // Check if category contains parameters
            var i = category.IndexOf('{');
        
            if (i == -1)
                return category;

            Span<char> brackets = stackalloc [] { '{', '}', '=' };
            var args            = context.ActionArguments;
            var categoryPieces  = category.AsSpan();
            var sb              = new StringBuilder(categoryPieces.Length);

            // Iterate through & replace parameters with values
            while (i > -1)
            {
                var segment = categoryPieces[0..i];
                var start   = categoryPieces[i];

                if (start == '{')
                {
                    // Add previous segment
                    sb.Append(segment);
                }
                else if (start == '}')
                {
                    if (args.TryGetValue(segment.ToString(), out var value))
                        // Append value
                        sb.Append(value);
                }
                // Check if there is a default value (=) designation after the name
                else if (start == '=' &&
                    categoryPieces.IndexOf('}') is int endOfDefault && endOfDefault > -1)
                {
                    if (args.TryGetValue(segment.ToString(), out var value))
                        // Append value
                        sb.Append(value);
                    else
                        sb.Append(categoryPieces[(i + 1)..endOfDefault]);

                    i = endOfDefault;
                }

                // Remove previous segment
                categoryPieces = categoryPieces[++i..];

                // Find next open/close bracket
                i = categoryPieces.IndexOfAny(brackets);
            }

            if (categoryPieces.Length > 0)
                sb.Append(categoryPieces);

            return sb.ToString();
        }

        /// <summary>
        /// Calculate a hashcode
        /// </summary>
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
                                result = result ^ GetStringHashCode(p.Name) ^ GetStringHashCode(argValue);
                                
                            else if (context.RouteData.Values.TryGetValue(p.Name, out var routeValue))
                                result = result ^ GetStringHashCode(p.Name) ^ GetStringHashCode(routeValue);
                        }
                    }
                    else
                    {
                        var parameters = VaryByParam.Split(';', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var arg in parameters)
                        {
                            if (context.ActionArguments.TryGetValue(arg, out var argValue))
                                result = result ^ GetStringHashCode(arg) ^ GetStringHashCode(argValue);

                            else if (context.RouteData.Values.TryGetValue(arg, out var routeValue))
                                result = result ^ GetStringHashCode(arg) ^ GetStringHashCode(routeValue);
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
                            result = result ^ GetStringHashCode(arg) ^ GetStringHashCode(value);
                    }
                }

                // Vary by some custom input
                if (VaryByCustom != null && 
                    typeof(IGlobalCacheExpiration).IsAssignableFrom(VaryByCustom) && 
                    Activator.CreateInstance(VaryByCustom) is IGlobalCacheExpiration expr)
                    result = result ^ GetStringHashCode(expr.GetId(context));

                return result;
            }
        }
    }
}