using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Wivuu.GlobalCache.Web
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class GlobalCacheClearAttribute : BaseCacheIdAttribute, IAsyncActionFilter, IAsyncResultFilter
    {
        const string ClearCacheId = "GlobalCache:Clear:CacheId";

        /// <summary>
        /// GlobalCacheClear attribute
        /// </summary>
        /// <param name="category">The prefix category of the cached item</param>
        public GlobalCacheClearAttribute(string category) : base (category)
        {
        }

        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var hash = CalculateHashCode(context);
            var id   = hash != 0
                ? new CacheId(CalculateCategory(context, Category), hash)
                : CacheId.ForCategory(CalculateCategory(context, Category));

            if (context.HttpContext.Items.TryGetValue(ClearCacheId, out var idsObj) &&
                idsObj is Stack<CacheId> ids)
                ids.Push(id);
            else
                context.HttpContext.Items.TryAdd(ClearCacheId, new Stack<CacheId>(new [] { id }));
            
            return next();
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            await next();

            // If response was successful & we have a configured storage provider
            if (httpContext.Response.StatusCode >= 200 && 
                httpContext.Response.StatusCode < 300 &&
                httpContext.Items.TryGetValue(ClearCacheId, out var idsObj) && 
                idsObj is Stack<CacheId> ids &&
                httpContext.RequestServices.GetService<IGlobalCache>() is IGlobalCache cache)
            {
                httpContext.Items.Remove(ClearCacheId);

                // Remove based on the id
                while (ids.TryPop(out var id))
                    await cache.InvalidateAsync(id);
            }
        }
    }
}