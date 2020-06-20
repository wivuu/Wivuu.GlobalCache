using System;
using System.IO;
using System.IO.Pipelines;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Wivuu.GlobalCache.AzureStorage
{
    public class BlobStorageProvider : IStorageProvider
    {
        static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(60);

        public BlobStorageProvider(StorageSettings settings)
        {
            if (settings.ConnectionString == null)
                throw new ArgumentNullException($"{nameof(BlobStorageProvider)} requires a connection string");

            this.Settings = settings;
            
            var blobServiceClient = new BlobServiceClient(settings.ConnectionString);
            
            this.ContainerClient = blobServiceClient.GetBlobContainerClient(settings.ContainerName);
        }

        public StorageSettings Settings { get; }
        protected BlobContainerClient ContainerClient { get; }

        private static string IdToString(CacheIdentity id) =>
            $"{id.Category}/{id.Hashcode}";

        public async Task<bool> ExistsAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient &&
                await blobClient.ExistsAsync(cancellationToken) is Azure.Response<bool> result)
                return result.Value;

            return false;
        }

        public Task<Stream?> OpenReadAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
            {
                var pipe = new System.IO.Pipelines.Pipe();
                
                _ = Task.Run(async () => 
                {
                    using var writerStream = pipe.Writer.AsStream(true);

                    await blobClient.DownloadToAsync(writerStream);
                    await pipe.Writer.CompleteAsync();
                });

                return Task.FromResult<Stream?>(pipe.Reader.AsStream(false));
            }

            return Task.FromResult(default(Stream));
        }

        public Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
                return blobClient.DeleteIfExistsAsync();

            return Task.CompletedTask;
        }

        public async Task<IAsyncDisposable> TryAcquireLockAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
            {
                var lease = new BlobLeaseClient(blobClient);

                await lease.AcquireAsync(LeaseTimeout, cancellationToken: cancellationToken);

                return new AsyncDisposable(() => lease.ReleaseAsync());
            }

            return AsyncDisposable.CompletedTask;
        }

        public Stream OpenWrite<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
            {
                var pipe = new Pipe();

                _ = blobClient.UploadAsync(pipe.Reader.AsStream(false));

                return pipe.Writer.AsStream(true);
            }

            throw new Exception("Failed to open write stream");
        }

        private struct AsyncDisposable : IAsyncDisposable
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
}