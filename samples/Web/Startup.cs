using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class GlobalCacheAttribute : Attribute, IAsyncActionFilter, IAsyncResultFilter
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
        public string ContentType { get; set; } = "application/json";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // TODO: Build an ID based on request, attribute settings, and timestamp
            var id = new CacheId(Category, 0);

            var settings = context.HttpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
            var storage  = settings.Value.StorageProvider;

            // if reader works, set `context.Result` to a GlobalCacheObjectResult
            if (await storage!.TryOpenRead(id, context.HttpContext.RequestAborted) is Stream stream)
                context.Result = new GlobalCacheObjectResult(stream);
            else
            {
                // Execute next and continue as normal
                await next();

                context.HttpContext.Items.Add("GlobalCache:CacheId", id);
            }
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            // IF the response is a GlobalCacheObjectResult, continue as normal
            if (context.Result is GlobalCacheObjectResult)
            {
                httpContext.Response.Headers["Content-Type"] = ContentType;

                await next();
            }
            else
            {
                var settings = httpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
                var storage  = settings.Value.StorageProvider;

                // OTHERWISE open an exclusive WRITE operation
                if (httpContext.Items.TryGetValue("GlobalCache:CacheId", out var idObj) && idObj is CacheId id &&
                    await storage!.TryOpenWrite(id, httpContext.RequestAborted) is Stream storageStream)
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
