using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
                // TODO: Return if blob is locked
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
                        using var writerStream = pipe.Writer.AsStream(true);

                        await blobClient.DownloadToAsync(writerStream, cancellationToken).ConfigureAwait(false);
                        pipe.Writer.Complete();
                    }
                    catch (Exception error)
                    {
                        pipe.Writer.Complete(error);
                    }
                });

                return pipe.Reader.AsStream();
            }

            return Stream.Null;
        }

        public Stream OpenWrite(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);

            return new OpenWriteStream(async stream =>
            {
                await using var lease = await LeaseAndTruncateAsync(id, blobClient, cancellationToken);

                await blobClient.UploadAsync(stream, 
                    conditions: new BlobRequestConditions { LeaseId = lease.Id }, 
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            });
        }

        public Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
                return blobClient.DeleteIfExistsAsync();

            return Task.CompletedTask;
        }

        private async Task<BlobLock> LeaseAndTruncateAsync(CacheIdentity id, BlobClient blobClient, CancellationToken cancellationToken = default)
        {
            var lease = new BlobLeaseClient(blobClient);

            await blobClient.UploadAsync(Stream.Null, overwrite: false, cancellationToken).ConfigureAwait(false);
            await lease.AcquireAsync(LeaseTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);

            return new BlobLock(lease.LeaseId, () => lease.ReleaseAsync());
        }

        private class BlobLock : AsyncDisposable
        {
            public BlobLock(string leaseId, Func<Task> done) : base(done)
            {
                Id = leaseId;
            }

            public string Id { get; }
        }
    }
}