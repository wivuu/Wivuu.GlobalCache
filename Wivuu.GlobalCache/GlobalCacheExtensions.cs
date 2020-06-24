using System;
using Wivuu.GlobalCache;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class GlobalCacheExtensions
    {
        public static IServiceCollection AddWivuuGlobalCache(this IServiceCollection collection, Action<GlobalCacheSettings> configure = default)
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