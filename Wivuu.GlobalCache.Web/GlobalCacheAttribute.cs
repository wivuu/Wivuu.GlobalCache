using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wivuu.GlobalCache.Web
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class GlobalCacheAttribute : Attribute, IAsyncActionFilter, IAsyncResultFilter
    {
        /// <summary>
        /// GlobalCache attribute
        /// </summary>
        /// <param name="category">The prefix category of the cached item</param>
        public GlobalCacheAttribute(string category)
        {
            Category = category;
        }

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
        /// Response content-type when a cached item is served
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// By default `DurationSecs` will simply floor the period, for example: 60 seconds will always start at the
        /// start of every minute, and 3,600 will always default to the start of every hour. Using `OffsetDurationSecs`
        /// offsets that by the input seconds, so -600 on duration 3600 will start the interval at 10 minutes prior 
        /// to beginning of the hour.
        /// </summary>
        public int OffsetDurationSecs { get; set; }

        int CalculateHashCode(ActionExecutingContext context)
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

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var settings    = httpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
            var ifNoneMatch = httpContext.Request.Headers["If-None-Match"];
            var storage     = settings.Value.StorageProvider;
            var id          = new CacheId(Category, CalculateHashCode(context), ifNoneMatch);

            // if reader works, set `context.Result` to a GlobalCacheObjectResult
            if (await storage!.TryOpenRead(id, httpContext.RequestAborted) is Stream stream)
            {
                // Output etag
                if (stream is IHasEtag etagContainer &&
                    etagContainer.ETag is string etag)
                {
                    httpContext.Response.Headers["ETag"] = etag;

                    // If requested etag is the same as the stream etag, return not modified
                    if (ifNoneMatch == etag)
                    {
                        httpContext.Response.StatusCode = 304;
                        context.Result = new EmptyResult();
                        return;
                    }
                }

                context.Result = new OkObjectResult(stream);
            }
            else
            {
                // Execute next and continue as normal
                await next();

                httpContext.Items.Add("GlobalCache:CacheId", id);
            }
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            // IF the CacheId is set, multiplex to persistent storage & output
            if (httpContext.Items.TryGetValue("GlobalCache:CacheId", out var idObj) && idObj is CacheId id)
            {
                var settings = httpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
                var storage  = settings.Value.StorageProvider;

                // Open exclusive write
                if (await storage!.TryOpenWrite(id, httpContext.RequestAborted) is StreamWithCompletion storageStream)
                {
                    using (storageStream)
                    {
                        // Create a PersistentBodyFeature which relays to WRITE stream AND to
                        // the original response writer
                        var responseWriter = httpContext.Features.Get<IHttpResponseBodyFeature>();

                        using var multistream = new MultiplexWriteStream(
                            responseWriter.Writer.AsStream(true), storageStream);

                        httpContext.Features.Set<IHttpResponseBodyFeature>(
                            new StreamResponseBodyFeature(multistream));

                        await next();
                    }

                    if (httpContext.Response.SupportsTrailers() &&
                        storageStream.Completion is Task<string> etagTask)
                    {
                        // Add etag to trailer
                        if (await etagTask is string etag)
                            httpContext.Response.AppendTrailer("ETag", etag);
                    }
                    else
                        await storageStream;
                }
                else
                    await next();
            }
            // OTHERWISE continue as normal
            else
            {
                httpContext.Response.Headers["Content-Type"] = ContentType;

                await next();
            }
        }
    }

    public interface IGlobalCacheExpiration
    {
        object GetId(ActionExecutingContext context);
    }
}