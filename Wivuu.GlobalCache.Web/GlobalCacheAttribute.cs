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
    public class GlobalCacheAttribute : BaseCacheIdAttribute, IAsyncActionFilter, IAsyncResultFilter
    {
        /// <summary>
        /// GlobalCache attribute
        /// </summary>
        /// <param name="category">The prefix category of the cached item</param>
        public GlobalCacheAttribute(string category) : base(category)
        {
        }

        /// <summary>
        /// Cache control: none, private or public (defaults to public)
        /// </summary>
        public CacheControlLevel CacheControlLevel { get; set; } = CacheControlLevel.Public;

        /// <summary>
        /// Response content-type when a cached item is served
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var settings    = httpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
            var ifNoneMatch = httpContext.Request.Headers["If-None-Match"];
            var storage     = settings.Value.StorageProvider;
            var id          = new CacheId(CalculateCategory(context, Category), CalculateHashCode(context), ifNoneMatch);

            if (CacheControlLevel != CacheControlLevel.None)
            {
                var level = CacheControlLevel == CacheControlLevel.Private ? "private" : "public";

                httpContext.Response.Headers["Cache-Control"] = DurationSecs > 0
                    ? $"{level}, max-age={DurationSecs}"
                    : level;
            }
                
            // if reader works, set `context.Result` to a GlobalCacheObjectResult
            if (await storage!.TryOpenRead(id, httpContext.RequestAborted) is Stream stream)
            {
                // Output etag
                if (CacheControlLevel != CacheControlLevel.None &&
                    stream is IHasEtag etagContainer &&
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
                    var hasTrailers = 
                        CacheControlLevel != CacheControlLevel.None &&
                        httpContext.Response.SupportsTrailers();

                    if (hasTrailers)
                        httpContext.Response.DeclareTrailer("ETag");

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

                    if (hasTrailers && storageStream.Completion is Task<string> etagTask)
                    {
                        // Add etag to trailer
                        if (await etagTask is string etag)
                            httpContext.Response.AppendTrailer("ETag", etag);
                        else
                            httpContext.Response.AppendTrailer("ETag", "\"\"");
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