using System;
using System.IO;
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

        public Task<Stream?> TryOpenRead(CacheId id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            EnsureDirectory(path);

            try
            {
                var result = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                return Task.FromResult<Stream?>(result);
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult<Stream?>(default);
            }
            catch (IOException ex)
            {
                // In use by another process
                if (Environment.OSVersion.Platform == PlatformID.Unix && ex.HResult == 11)
                    return Task.FromResult<Stream?>(default);
                if (ex.HResult == -2147024864)
                    return Task.FromResult<Stream?>(default);

                throw new Exception($"ex: {ex.HResult}", ex);
            }
        }

        public Task<StreamWithCompletion?> TryOpenWrite(CacheId id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            EnsureDirectory(path);

            try 
            {
                var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

                return Task.FromResult<StreamWithCompletion?>(
                    new StreamWithCompletion(
                        fs,
                        Task.CompletedTask
                    )
                );
            }
            catch (IOException ex)
            {
                if (ex.HResult == -2147024864) // In use by another process
                    return Task.FromResult<StreamWithCompletion?>(default);

                throw;
            }
        }
    }
}