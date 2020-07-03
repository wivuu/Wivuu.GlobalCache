using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface IStorageProvider
    {
        /// <summary>
        /// Get or create a cached item in the underlying storage provider.
        /// </summary>
        /// <param name="id">The global cache identity of the cached item</param>
        /// <param name="onRead">If provided will deserialize the object if available</param>
        /// <param name="onWrite">If provided will serialize the input object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="T">Type of object read/write</typeparam>
        Task<T> OpenReadWriteAsync<T>(CacheId id,
                                      Func<Stream, Task<T>>? onRead = default,
                                      Func<Stream, Task<T>>? onWrite = default,
                                      CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove one or more items from cache based on identity
        /// </summary>
        /// <param name="id">Identity of items(s)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if 1 or more items were removed</returns>
        Task<bool> RemoveAsync(CacheId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Try to open a read stream in the underlying storage provider.
        /// </summary>
        /// <param name="id">The global cache identity of the cached item</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream or null</returns>
        Task<Stream?> TryOpenRead(CacheId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Try to open a write stream in the underlying storage provider.
        /// </summary>
        /// <param name="id">The global cache identity of the cached item</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream or null</returns>
        Task<StreamWithCompletion?> TryOpenWrite(CacheId id, CancellationToken cancellationToken = default);
    }
}