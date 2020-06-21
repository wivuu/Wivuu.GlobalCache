using System;
using System.IO;
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
            $"{id.Category}/{id.Hashcode}.dat";

        public async Task<bool> ExistsAsync<T>(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient &&
                await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false) is Azure.Response<bool> result)
                return result.Value;

            return false;
        }

        public Stream OpenRead(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
            {
                var pipe = new System.IO.Pipelines.Pipe();
                
                _ = Task.Run(async () => 
                {
                    try
                    {
                        using var writerStream = pipe.Writer.AsStream();

                        await blobClient.DownloadToAsync(writerStream, cancellationToken).ConfigureAwait(false);
                        await pipe.Writer.CompleteAsync().ConfigureAwait(false);
                    }
                    catch (Exception error)
                    {
                        await pipe.Writer.CompleteAsync(error).ConfigureAwait(false);
                    }
                });

                return pipe.Reader.AsStream();
            }

            return Stream.Null;
        }

        public Stream OpenWrite(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
                return new ReadWriteStream(stream => 
                    blobClient.UploadAsync(stream, overwrite: true, cancellationToken)
                );

            throw new Exception("Failed to open write stream");
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

                await lease.AcquireAsync(LeaseTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);

                return new AsyncDisposable(() => lease.ReleaseAsync());
            }

            return AsyncDisposable.CompletedTask;
        }
    }
}