using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class StorageTests
    {
        [Fact]
        public async Task TestAzureReadWrite()
        {
            var azStore = new BlobStorageProvider(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            var id  = new CacheIdentity("Test", 1);
            var str = "hello world" + Guid.NewGuid();

            await azStore.RemoveAsync(id);

            // Write file
            using (var stream = azStore.OpenWrite(id))
            {
                var data = Encoding.Default.GetBytes(str);

                await stream.WriteAsync(data);
            }

            await Task.Delay(100);

            // Read file
            using (var stream = azStore.OpenRead(id))
            {
                using var sr = new StreamReader(stream);
                var data     = await sr.ReadToEndAsync();

                Assert.Equal(str, data);
            }
        }
    }
}
