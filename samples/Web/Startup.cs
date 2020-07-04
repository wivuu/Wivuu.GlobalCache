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
using Microsoft.Extensions.Logging;
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

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class GlobalCacheAttribute : Attribute, IAsyncActionFilter, IAsyncResultFilter
    {
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
        /// The cached item will be valid for this many seconds. Leave 
        /// unspecified or specify `0` for infinite duration
        /// </summary>
        public int DurationSecs { get; set; }

        /// <summary>
        /// Response content-type when a cached item is served
        /// </summary>
        public string ContentType { get; set; } = "application/json";

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
                    var date  = new DateTime(ticks * floor, DateTimeKind.Utc);

                    result = result ^ this.GetHashCode();
                }

                var log = context.HttpContext.RequestServices.GetService<ILogger<GlobalCacheAttribute>>();

                // Include params
                if (VaryByParam is string varyParam)
                {
                    if (varyParam == "*")
                    {
                        foreach (var p in context.ActionDescriptor.Parameters)
                        {
                            if (!p.BindingInfo.BindingSource.IsFromRequest)
                                continue;

                            if (context.ActionArguments.TryGetValue(p.Name, out var value))
                                result = result ^ CacheId.GetStringHashCode(p.Name) ^ value.GetHashCode();
                        }
                    }
                    else
                    {
                        var parameters = VaryByParam.Split(';', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var arg in parameters)
                        {
                            if (context.ActionArguments.TryGetValue(arg, out var value))
                                result = result ^ CacheId.GetStringHashCode(arg) ^ value.GetHashCode();
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
                            result = result ^ CacheId.GetStringHashCode(arg) ^ value.GetHashCode();
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
            var id       = new CacheId(Category, CalculateHashCode(context));
            var settings = context.HttpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
            var storage  = settings.Value.StorageProvider;

            // if reader works, set `context.Result` to a GlobalCacheObjectResult
            if (await storage!.TryOpenRead(id, context.HttpContext.RequestAborted) is Stream stream)
                context.Result = new ObjectResult(stream);
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

            // IF the CacheId is set, multiplex to persistent storage & output
            if (httpContext.Items.TryGetValue("GlobalCache:CacheId", out var idObj) && idObj is CacheId id)
            {
                var settings = httpContext.RequestServices.GetService<IOptions<GlobalCacheSettings>>();
                var storage  = settings.Value.StorageProvider;

                // Open exclusive write
                if (await storage!.TryOpenWrite(id, httpContext.RequestAborted) is Stream storageStream)
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
