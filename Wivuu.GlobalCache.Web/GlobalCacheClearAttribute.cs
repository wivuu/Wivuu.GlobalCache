using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Wivuu.GlobalCache.Web
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class GlobalCacheClearAttribute : BaseCacheIdAttribute, IAsyncActionFilter, IAsyncResultFilter
    {
        /// <summary>
        /// GlobalCacheClear attribute
        /// </summary>
        /// <param name="category">The prefix category of the cached item</param>
        public GlobalCacheClearAttribute(string category)
        {
            Category = category;
        }

        public string Category { get; }

        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var id = new CacheId(Category, CalculateHashCode(context));
            context.HttpContext.Items.Add("GlobalCache:CacheId", id);
            
            return next();
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            await next();

            // If response was successful & we have a configured storage provider
            if (httpContext.Response.StatusCode >= 200 && 
                httpContext.Response.StatusCode < 300 &&
                httpContext.Items.TryGetValue("GlobalCache:CacheId", out var idObj) && idObj is CacheId id &&
                httpContext.RequestServices.GetService<IStorageProvider>() is var storage)
            {
                // Remove based on the id
                await storage.RemoveAsync(id);
            }
        }
    }
}