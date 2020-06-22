using System;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache.AzureStorage
{
    internal class AsyncDisposable : IAsyncDisposable
    {
        public AsyncDisposable(Func<Task> done)
        {
            Done = done;
        }

        public static IAsyncDisposable CompletedTask { get; } 
            = new AsyncDisposable(() => Task.CompletedTask);

        public Func<Task> Done { get; }

        public ValueTask DisposeAsync() => new ValueTask(Done());
    }
}