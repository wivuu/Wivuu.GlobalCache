using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    internal class FileStorageProvider : IStorageProvider
    {
        /// <summary>
        /// File storage provider
        /// </summary>
        /// <param name="root">Directory to store cached items in</param>
        public FileStorageProvider(string? root = default)
        {
            Root = root ?? Environment.CurrentDirectory;
        }

        static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(60);

        protected string Root { get; }

        protected string IdToString(CacheId id) =>
            Path.Combine(Root, id.IsCategory ? id.ToString() : $"{id}.dat");

        public void EnsureDirectory(string path)
        {
            var dirName = Path.GetDirectoryName(path);

            Directory.CreateDirectory(dirName);
        }

        public Task<bool> RemoveAsync(CacheId id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (id.IsCategory)
            {
                try
                {
                    Directory.Delete(path, true);

                    return Task.FromResult(true);
                }
                catch (IOException)
                {
                    return Task.FromResult(false);
                }
            }
            else
            {

                try
                {
                    File.Delete(path);
                    return Task.FromResult(true);
                }
                catch (IOException)
                {
                    // noop
                    return Task.FromResult(false);
                }
            }
        }

        public async Task<T> OpenReadWriteAsync<T>(CacheId id,
                                                   Func<Stream, Task<T>>? onRead = null,
                                                   Func<Stream, Task<T>>? onWrite = null,
                                                   CancellationToken cancellationToken = default)
        {
            if (id.IsCategory)
                throw new ArgumentException("Cannot read/write to a category");

            var path = IdToString(id);

            EnsureDirectory(path);

            var retries = new RetryHelper(1, 500, totalMaxDelay: LeaseTimeout);

            // Wait for break in traffic
            do
            {
                // Try to read the file
                if (onRead != null)
                {
                    var pipe = new Pipe();

                    try
                    {
                        using var fileStream   = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var cts          = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        using var writerStream = pipe.Writer.AsStream(true);

                        var readerTask = Task.Run(() => onRead(pipe.Reader.AsStream(true)));

                        await Task.WhenAll(
                            readerTask
                                .ContinueWith(t => pipe.Reader.Complete(t.Exception?.GetBaseException())),
                            fileStream
                                .CopyToAsync(writerStream, cancellationToken)
                                .ContinueWith(t => pipe.Writer.Complete(t.Exception?.GetBaseException()))
                        ).ConfigureAwait(false);

                        if (readerTask.IsCompletedSuccessfully)
                            return readerTask.Result;
                    }
                    catch (IOException e)
                    {
                        if (e is FileNotFoundException) {}
                        else
                            continue;
                    }
                }

                // Try to WRITE file
                if (onWrite != null)
                {
                    // Create a new pipe
                    var pipe = new Pipe();

                    using var fileStream   = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    using var cts          = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    using var readerStream = pipe.Reader.AsStream(true);

                    var writerTask = Task.Run(() => onWrite(pipe.Writer.AsStream(true)));

                    await Task.WhenAll(
                        writerTask
                            .ContinueWith(t => pipe.Writer.Complete(t.Exception?.GetBaseException())),
                        readerStream
                            .CopyToAsync(fileStream)
                            .ContinueWith(t => pipe.Reader.Complete(t.Exception?.GetBaseException()))
                    );

                    if (writerTask.IsCompletedSuccessfully)
                        return writerTask.Result;
                }

                if (await retries.DelayAsync(cancellationToken).ConfigureAwait(false) == false)
                    throw new TimeoutException();
            }
            while (!cancellationToken.IsCancellationRequested);

            throw new TaskCanceledException();
        }

        public Task<Stream?> TryOpenRead(CacheId id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            EnsureDirectory(path);

            return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public Task<StreamWithCompletion?> TryOpenWrite(CacheId id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            EnsureDirectory(path);

            return Task.FromResult<StreamWithCompletion?>(
                new StreamWithCompletion(
                    new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None),
                    Task.CompletedTask
                )
            );
        }
    }
}