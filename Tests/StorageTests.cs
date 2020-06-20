using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class StorageTests
    {
        [Fact]
        public async Task TestAzureStorage()
        {
            var azStore = new BlobStorageProvider(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            // Write file
            using (var stream = azStore.OpenWrite<string>(new CacheIdentity("Test")))
            using (var sr     = new StreamWriter(stream))
            {
                await sr.WriteAsync("This is a test!");
            }

            // 
        }
    }
}
