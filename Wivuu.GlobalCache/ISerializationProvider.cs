using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface ISerializationProvider
    {
        /// <summary>
        /// Serialize input object to output stream
        /// </summary>
        /// <param name="input">The object to serialize</param>
        /// <param name="output">The stream to serialize object to</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="T">Type of object to serialize</typeparam>
        Task SerializeToStreamAsync<T>(T input,
                                       Stream output,
                                       CancellationToken cancellationToken = default);

        /// <summary>
        /// Deserialize an object from the input stream
        /// </summary>
        /// <param name="input">The stream to retrieve the object from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="T">Type of object to deserialize</typeparam>
        /// <returns>Deserialized object</returns>
        Task<T> DeserializeFromStreamAsync<T>(Stream input,
                                              CancellationToken cancellationToken = default);
    }
}