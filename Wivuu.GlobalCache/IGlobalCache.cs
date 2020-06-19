using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public interface IGlobalCache
    {
        Task<T> GetOrCreateAsync<T>(string category, int hashcode, Func<GlobalCacheEntrySettings, Task<T>> generator);

        IAsyncEnumerable<T> GetOrCreateAsync<T>(string category, int hashcode, Func<GlobalCacheEntrySettings, IAsyncEnumerable<T>> generator);

        Task InvalidateAsync(string category, int? hashcode = null);

        Task CreateAsync<T>(string category, int hashcode, Func<GlobalCacheEntrySettings, Task<T>> generator);

        Task CreateAsync<T>(string category, int hashcode, Func<GlobalCacheEntrySettings, IAsyncEnumerable<T>> generator);

        Task<T> GetAsync<T>(string category, int hashcode);
    }
}
