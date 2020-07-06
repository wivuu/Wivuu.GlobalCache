using System;
using Wivuu.GlobalCache;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class GlobalCacheExtensions
    {
        /// <summary>
        /// Adds global distributed access caching mechanism into DI
        /// </summary>
        /// <param name="collection">The service collection being built</param>
        /// <param name="configure">Optionally configure global cache settings</param>
        public static IServiceCollection AddWivuuGlobalCache(this IServiceCollection collection,
                                                             Action<GlobalCacheSettings>? configure = default)
        {
            collection.Configure<GlobalCacheSettings>(options =>
            {
                if (configure != null)
                    configure(options);

                if (options.StorageProvider == null)
                    options.StorageProvider = new FileStorageProvider();

                if (options.SerializationProvider == null)
                    options.SerializationProvider = new JsonSerializationProvider();
            });

            collection.AddSingleton<IGlobalCache, DefaultGlobalCache>();

            return collection;
        }
    }
}