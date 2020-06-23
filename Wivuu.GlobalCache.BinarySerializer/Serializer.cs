using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

namespace Wivuu.GlobalCache.BinarySerializer
{
    public class Serializer : ISerializationProvider
    {
        public async Task<T> DeserializeFromStreamAsync<T>(Stream input, CancellationToken cancellationToken = default)
        {
            return await MessagePackSerializer.DeserializeAsync<T>(
                input, 
                options: MessagePack.Resolvers.ContractlessStandardResolverAllowPrivate.Options,
                cancellationToken: cancellationToken);
        }

        public async IAsyncEnumerable<T> DeserializeManyFromStreamAsync<T>(Stream input, [EnumeratorCancellation]CancellationToken cancellationToken = default)
        {
            var options = MessagePack.Resolvers.ContractlessStandardResolverAllowPrivate.Options;

            while (!cancellationToken.IsCancellationRequested)
            {
                T item;
                
                try
                {
                    item = await MessagePackSerializer.DeserializeAsync<T>(
                        input, 
                        options: options,
                        cancellationToken: cancellationToken);
                }
                catch (MessagePack.MessagePackSerializationException e)
                {
                    if (e.GetBaseException() is EndOfStreamException)
                        break;
                    else
                        throw;
                }

                yield return item;
            }
        }

        public async Task SerializeToStreamAsync<T>(T input, Stream output, CancellationToken cancellationToken = default)
        {
            await MessagePackSerializer.SerializeAsync(
                output, input,
                options: MessagePack.Resolvers.ContractlessStandardResolverAllowPrivate.Options,
                cancellationToken: cancellationToken);
        }

        public async Task SerializeToStreamAsync<T>(IAsyncEnumerable<T> input, Stream output, CancellationToken cancellationToken = default)
        {
            var options = MessagePack.Resolvers.ContractlessStandardResolverAllowPrivate.Options;

            await foreach (var i in input)
            {
                await MessagePackSerializer.SerializeAsync(
                    output, i,
                    options: options,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
