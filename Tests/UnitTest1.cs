using System;
using Microsoft.Extensions.DependencyInjection;
using Wivuu.GlobalCache;
using Xunit;

namespace Tests
{
    public class UnitTest1
    {
        public UnitTest1()
        {
            var collection = new ServiceCollection();

            collection.Configure<GlobalCacheEntrySettings>(options =>
            {
            });

            // TODO: Register services
            // collection.AddSingleton<IGlobalCache, AzureBlobGlobalCache>();
            
            this.Services = collection.BuildServiceProvider();
        }

        public ServiceProvider Services { get; }

        [Fact]
        public void Test1()
        {
            using var scope = Services.CreateScope();
        }
    }
}
