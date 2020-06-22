using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
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

        public async Task<CacheStatus> ExistsAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);

            try
            {
                var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                // Check if blob is locked
                return properties.Value.LeaseState == LeaseState.Leased 
                    ? CacheStatus.Locked 
                    : CacheStatus.Exists;
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                    return CacheStatus.NotExists;
                else
                    throw;
            }
        }

        public async Task<bool> WaitForLock(CacheIdentity id, string? etag = default, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);

            try
            {
                using var retries = new RetryHelper(2, maxDelay: 100, totalMaxDelay: LeaseTimeout);

                do
                {
                    var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Check if blob is locked & not partial
                    if (properties.Value.Metadata.TryGetValue("partial", out _) == false && 
                        properties.Value.LeaseState != LeaseState.Leased)
                        break;

                    if (!await retries.DelayAsync())
                        // Out of retries
                        throw new Exception("Lock past ");
                }
                while (!cancellationToken.IsCancellationRequested);

                return true;
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                    return false;

                throw;
            }
        }

        public Stream OpenReadAsync(CacheIdentity id, CancellationToken cancellationToken = default)
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

        public async Task<Stream> OpenWriteAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);
            var lease      = await LeaseAndTruncateAsync(id, blobClient, cancellationToken);

            return new OpenWriteStream(async stream =>
            {
                await using (lease)
                {
                    await blobClient.UploadAsync(stream, 
                        conditions: new BlobRequestConditions { LeaseId = lease.Id }, 
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            });
        }

        public async Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);

            await blobClient.DeleteIfExistsAsync();
        }

        private async Task<BlobLock> LeaseAndTruncateAsync(CacheIdentity id, BlobClient blobClient, CancellationToken cancellationToken = default)
        {
            var lease = new BlobLeaseClient(blobClient);

            await blobClient.UploadAsync(
                Stream.Null,
                metadata: new Dictionary<string, string>
                {
                    ["partial"] = "1"
                }, 
                cancellationToken: cancellationToken).ConfigureAwait(false);

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

    public enum CacheStatus
    {
        NotExists,
        Locked,
        Exists,
    }
}