using System.IO;
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

        public async Task SerializeToStreamAsync<T>(T input, Stream output, CancellationToken cancellationToken = default)
        {
            await MessagePackSerializer.SerializeAsync(
                output, input,
                options: MessagePack.Resolvers.ContractlessStandardResolverAllowPrivate.Options,
                cancellationToken: cancellationToken);
        }
    }
}
