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
            if (configure != null)
                collection.Configure<GlobalCacheSettings>(settings =>
                {
                    configure(settings);
                    
                    if (!(settings.StorageProvider is null))
                        collection.AddSingleton<IStorageProvider>(settings.StorageProvider);
                });
            else
            {
                collection.AddSingleton<IStorageProvider, FileStorageProvider>();
                collection.Configure<GlobalCacheSettings>(settings => {});
            }

            collection.AddSingleton<IGlobalCache, DefaultGlobalCache>();

            return collection;
        }
    }
}