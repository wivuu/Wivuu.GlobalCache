using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Wivuu.GlobalCache.AzureStorage")]
[assembly: InternalsVisibleTo("Wivuu.GlobalCache.BinarySerializer")]
[assembly: InternalsVisibleTo("Wivuu.GlobalCache.Web")]
[assembly: InternalsVisibleTo("Tests")]
#if DEBUG
[assembly: InternalsVisibleTo("Web")]
#endif

namespace Wivuu.GlobalCache
{
    public interface IGlobalCache
    {
        /// <summary>
        /// The configured serialization provider
        /// </summary>
        ISerializationProvider SerializationProvider { get; }
        
        /// <summary>
        /// The configured storage provider
        /// </summary>
        IStorageProvider StorageProvider { get; }

        /// <summary>
        /// Gets item from global cache; only invoking generator if the item is not cached
        /// </summary>
        /// <param name="id">Id of item to retrieve (or create)</param>
        /// <param name="generator">If item is not present, generates item</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="T">Type of item</typeparam>
        /// <returns>Instance of <typeparamref name="T"/></returns>
        Task<T> GetOrCreateAsync<T>(CacheId id,
                                    Func<Task<T>> generator,
                                    CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates item in global cache
        /// </summary>
        /// <param name="id">Id of item to create</param>
        /// <param name="generator">Item generator</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="T">Type of item</typeparam>
        Task CreateAsync<T>(CacheId id,
                            Func<Task<T>> generator,
                            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets item from global cache - WARNING: May blocks or timeout based on underlying provider, if
        /// the cached item is not present
        /// </summary>
        /// <param name="id">Id of item to retrieve</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="T">Type of item</typeparam>
        /// <returns>Instance of <typeparamref name="T"/></returns>
        Task<T> GetAsync<T>(CacheId id,
                            CancellationToken cancellationToken = default);

        /// <summary>
        /// Globally invalidates by cache id
        /// </summary>
        /// <param name="id">Id or category to invalidate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InvalidateAsync(CacheId id,
                             CancellationToken cancellationToken = default);
    }
}
