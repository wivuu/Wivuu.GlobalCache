using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Wivuu.GlobalCache;

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
            var cache = httpContext.RequestServices.GetRequiredService<IGlobalCache>();
            var logger = httpContext.RequestServices.GetService<ILogger<GlobalCacheAttribute>>();

            // TODO: Build an ID based on request, attribute settings, and timestamp
            var id          = new CacheId(Category, 0);

            // TODO: Only execute if cached item is not present
            {
                // var ms = new MemoryStream();
                // httpContext.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(ms));

                await next();
            }

            // Otherwise replace with custom response

            // var response = context.HttpContext.Response;

            // stream.Seek(0, SeekOrigin.Begin);

            // TODO: Somehow retrieve the correct content-type
            // response.Headers["Content-Type"] = "application/json";

            // using var writer = response.BodyWriter.AsStream();
            // await stream.CopyToAsync(writer, context.HttpContext.RequestAborted);
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var logger = httpContext.RequestServices.GetService<ILogger<GlobalCacheAttribute>>();

            // If the item is cached
            // if (false)
            {
                var originalResponse = httpContext.Features.Get<IHttpResponseBodyFeature>();
                using var writer = originalResponse.Writer.AsStream();

                var newResponse = new MyStreamResponseBodyFeature(writer);
                httpContext.Features.Set<IHttpResponseBodyFeature>(newResponse);
                
                // 1. Serializes to configured output
                await next();
            }
            // else
            // {
            //     await next();
            // }
        }
    }

    public class MyStreamResponseBodyFeature : StreamResponseBodyFeature
    {
        public MyStreamResponseBodyFeature(Stream stream) : base(stream)
        {
        }
    }
}
