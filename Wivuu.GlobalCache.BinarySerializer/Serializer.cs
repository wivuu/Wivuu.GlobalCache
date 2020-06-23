using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BinaryPack;

namespace Wivuu.GlobalCache.BinarySerializer
{
    public class Serializer : ISerializationProvider
    {
        public Task<T> DeserializeFromStreamAsync<T>(Stream input, CancellationToken cancellationToken = default)
            where T : new()
        {
            var result = BinaryConverter.Deserialize<T>(input);

            return Task.FromResult(result);
        }

        public IAsyncEnumerable<T> DeserializeManyFromStreamAsync<T>(Stream input, CancellationToken cancellationToken = default)
            where T : new()
        {
            throw new NotImplementedException();
        }

        public Task SerializeToStreamAsync<T>(T input, Stream output, CancellationToken cancellationToken = default)
            where T : new()
        {
            BinaryConverter.Serialize(input, output);

            return Task.CompletedTask;
        }

        public Task SerializeToStreamAsync<T>(IAsyncEnumerable<T> input, Stream output, CancellationToken cancellationToken = default)
            where T : new()
        {
            throw new NotImplementedException();
        }
    }
}
