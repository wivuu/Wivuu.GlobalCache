using System;
using System.IO;
using System.Text;
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

            var id  = new CacheIdentity("Test");
            var str = "hello world" + Guid.NewGuid();

            // Write file
            await using (var stream = azStore.OpenWrite(id))
            {
                var data = Encoding.Default.GetBytes(str);

                await stream.WriteAsync(data);
            }

            // Read file
            await using (var stream = azStore.OpenRead(id))
            {
                using var sr = new StreamReader(stream);
                var data     = await sr.ReadToEndAsync();

                Assert.Equal(str, data);
            }
        }
    }
}
