using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface ISerializationProvider
    {
        Task SerializeToStreamAsync<T>(T input,
                                       Stream output,
                                       CancellationToken cancellationToken = default)
            where T : new();

        Task SerializeToStreamAsync<T>(IAsyncEnumerable<T> input,
                                       Stream output,
                                       CancellationToken cancellationToken = default)
            where T : new();

        Task<T> DeserializeFromStreamAsync<T>(Stream input,
                                              CancellationToken cancellationToken = default)
            where T : new();

        IAsyncEnumerable<T> DeserializeManyFromStreamAsync<T>(Stream input,
                                                              CancellationToken cancellationToken = default)
            where T : new();
    }
}