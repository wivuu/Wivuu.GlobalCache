using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;

namespace Web
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvcCore();

            services.AddWivuuGlobalCache(options =>
            {
                var connString = "UseDevelopmentStorage=true";
                    
                var container = new Azure.Storage.Blobs.BlobContainerClient(connString, "samplecache");
                container.CreateIfNotExists();

                options.StorageProvider       = new Wivuu.GlobalCache.AzureStorage.BlobStorageProvider(container);
                options.SerializationProvider = new Wivuu.GlobalCache.BinarySerializer.BinarySerializationProvider();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // app.UseGlobalCacheMiddleware();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });
        }
    }

    // public static class GlobalCacheMiddleware
    // {
    //     public static IApplicationBuilder UseGlobalCacheMiddleware(this IApplicationBuilder app)
    //     {
    //         app.Use(async (context, next) =>
    //         {
    //             await next.Invoke();
    //         });

    //         return app;
    //     }
    // }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class GlobalCacheAttribute : 
        // ActionFilterAttribute
        Attribute, IAsyncActionFilter, IAsyncResultFilter
    {
        public GlobalCacheAttribute(string category)
        {
            Category = category;
        }

        public string Category { get; }
        public string? VaryByParam { get; set; }
        public string? VaryByHeader { get; set; }
        public string? VaryByContentEncoding { get; set; }
        public Type? VaryByCustom { get; set; }
        public int DurationSecs { get; set; }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var logger      = httpContext.RequestServices.GetService<ILogger<GlobalCacheAttribute>>();

            // TODO: Build an ID based on request, attribute settings, and timestamp
            var id = new CacheId(Category, 0);

            var settings = httpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
            var storage  = settings.Value.StorageProvider as BlobStorageProvider;

            // if reader works, set `context.Result` to a GlobalCacheObjectResult
            if (await storage!.TryOpenRead(id, context.HttpContext.RequestAborted) is Stream stream)
                context.Result = new GlobalCacheObjectResult(stream);
            else
            {
                context.HttpContext.Items.Add("GlobalCache:CacheId", id);

                // Execute next and continue as normal
                await next();
            }
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var logger      = httpContext.RequestServices.GetService<ILogger<GlobalCacheAttribute>>();
            
            // IF the response is a GlobalCacheObjectResult, continue as normal
            if (context.Result is GlobalCacheObjectResult)
                await next();
            else
            {
                var settings = httpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
                var storage  = settings.Value.StorageProvider as BlobStorageProvider;

                // OTHERWISE open an exclusive WRITE operation
                // if (await storage.OpenExclusiveWrite() is var storageWriter)
                if (context.HttpContext.Items.TryGetValue("GlobalCache:CacheId", out var idObj) && idObj is CacheId id &&
                    await storage!.TryOpenWrite(id, context.HttpContext.RequestAborted) is Stream storageStream)
                {
                    // Create a PersistentBodyFeature which relays to WRITE stream AND to 
                    // the original response writer
                    var responseWriter = httpContext.Features.Get<IHttpResponseBodyFeature>();

                    using var multistream = new MultiplexWriteStream(
                        responseWriter.Writer.AsStream(true), storageStream);

                    httpContext.Features.Set<IHttpResponseBodyFeature>(
                        new StreamResponseBodyFeature(multistream)
                    );

                    try 
                    {
                        // Invoke NEXT
                        await next();
                    }
                    finally
                    {
                        storageStream.Dispose();
                    }
                }
                else
                    await next();
            }
        }
    }

    public class GlobalCacheObjectResult : ObjectResult
    {
        public GlobalCacheObjectResult(Stream value) : base(value)
        {
        }
    }
}
