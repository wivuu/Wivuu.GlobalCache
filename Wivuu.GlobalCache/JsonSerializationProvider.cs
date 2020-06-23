using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public class JsonSerializationProvider : ISerializationProvider
    {
        public Task<T> DeserializeFromStreamAsync<T>(Stream input, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task SerializeToStreamAsync<T>(T input, Stream output, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}