using System;
using System.IO;
using System.Threading.Tasks;
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
    }

    class TestItem
    {
        public int Item { get; set; }
        public string StrSomething { get; set; } = "This is a test";
    }
}
