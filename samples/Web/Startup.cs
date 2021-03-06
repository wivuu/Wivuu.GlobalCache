using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
}
