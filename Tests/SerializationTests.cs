using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Wivuu.GlobalCache;
using Xunit;

namespace Tests
{
    public class SerializationTests
    {
        [Fact]
        public async Task TestBinarySerializer()
        {
            var serializer = new Wivuu.GlobalCache.BinarySerializer.Serializer();

            var data = new TestItem { Item = 5, StrSomething = "Hello!" };
            using var ms = new MemoryStream();

            await serializer.SerializeToStreamAsync(data, ms);
            ms.Position = 0; // Rewind stream

            var resultData = await serializer.DeserializeFromStreamAsync<TestItem>(ms);

            Assert.NotNull(resultData);
            Assert.Equal(data.Item, resultData.Item);
            Assert.Equal(data.StrSomething, resultData.StrSomething);
        }

        [Fact]
        public async Task TestBinaryArraySerializer()
        {
            var serializer = new Wivuu.GlobalCache.BinarySerializer.Serializer();
            
            using var ms = new MemoryStream();
            const int num = 100_000;
            
            await serializer.SerializeToStreamAsync(GenerateItems(num), ms);
            ms.Position = 0; // Rewind stream

            var stored = 0;
            await foreach (var i in serializer.DeserializeManyFromStreamAsync<int>(ms))
            {
                ++stored;
            }

            Assert.Equal(num, stored);

            async IAsyncEnumerable<int> GenerateItems(int count)
            {
                for (var i = 0; i < count; ++i)
                {
                    await Task.Yield();
                    yield return i;
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
