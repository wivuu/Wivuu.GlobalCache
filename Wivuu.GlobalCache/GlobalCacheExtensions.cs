using System;
using Wivuu.GlobalCache;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class GlobalCacheExtensions
    {
        /// <summary>
        /// Adds global distributed caching mechanism into DI
        /// </summary>
        /// <param name="collection">The service collection being built</param>
        /// <param name="configure">Optionally configure global cache settings</param>
        public static IServiceCollection AddWivuuGlobalCache(this IServiceCollection collection,
                                                             Action<GlobalCacheSettings>? configure = default)
        {
            if (configure != null)
                collection.Configure<GlobalCacheSettings>(configure);
            else
                collection.Configure<GlobalCacheSettings>(settings => {});

            collection.AddSingleton<IGlobalCache, DefaultGlobalCache>();

            return collection;
        }
    }
}