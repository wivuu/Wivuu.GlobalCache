using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Xunit;

namespace Tests
{
    public class SerializationTests
    {
        [Theory]
        [InlineData(typeof(JsonSerializationProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.Serializer))]
        public async Task TestSerializer(Type serializerType)
        {
            if (!(Activator.CreateInstance(serializerType) is ISerializationProvider serializer))
                throw new Exception($"{serializerType} is not a serialization provider");

            var data = new TestItem { Item = 5, StrSomething = "Hello!" };
            using var ms = new MemoryStream();

            await serializer.SerializeToStreamAsync(data, ms);
            ms.Position = 0; // Rewind stream

            var resultData = await serializer.DeserializeFromStreamAsync<TestItem>(ms);

            Assert.NotNull(resultData);
            Assert.Equal(data.Item, resultData.Item);
            Assert.Equal(data.StrSomething, resultData.StrSomething);
        }

        [Theory]
        [InlineData(typeof(JsonSerializationProvider))]
        [InlineData(typeof(Wivuu.GlobalCache.BinarySerializer.Serializer))]
        public async Task TestAzureSerializers(Type serializerType)
        {
            if (!(Activator.CreateInstance(serializerType) is ISerializationProvider serializer))
                throw new Exception($"{serializerType} is not a serialization provider");

            var azStore = new BlobStorageProvider(new StorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true"
            });

            await azStore.EnsureContainerAsync();

            var id = new CacheIdentity(serializerType.Name, 5);
            await azStore.RemoveAsync(id);

            // Write data
            var written = await azStore.OpenReadWriteAsync(id, onWrite: async stream =>
            {
                // Open database and query data
                var items = new List<string>();
                
                await foreach (var item in GetData())
                    items.Add(item);

                _ = serializer.SerializeToStreamAsync(items, stream);

                return items;
            });

            Assert.NotNull(written);
            Assert.NotEmpty(written);

            // Read data
            var read = await azStore.OpenReadWriteAsync(id, onRead: async stream =>
                await serializer.DeserializeFromStreamAsync<List<string>>(stream)
            );

            Assert.NotNull(read);
            Assert.Equal(written.Count, read.Count);

            for (var i = 0; i < written.Count; ++i)
                Assert.Equal(written[i], read[i]);

            async IAsyncEnumerable<string> GetData()
            {
                for (var i = 0; i < 1000; ++i)
                {
                    await Task.Yield();
                    yield return $"Item {i}";
                }
            }
        }
    }

    public class TestItem
    {
        public int Item { get; set; }
        public string StrSomething { get; set; } = "This is a test";
    }
}
