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
                                       CancellationToken cancellationToken = default);

        Task<T> DeserializeFromStreamAsync<T>(Stream input,
                                              CancellationToken cancellationToken = default);
    }
}