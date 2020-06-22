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
    public class BlobStorageProvider2 : IStorageProvider
    {
        public delegate Task WithLeaseDelegate(BlobClient client, string lease);

        public BlobStorageProvider2(StorageSettings settings)
        {
            if (settings.ConnectionString == null)
                throw new ArgumentNullException($"{nameof(BlobStorageProvider)} requires a connection string");

            this.Settings = settings;

            var blobServiceClient = new BlobServiceClient(settings.ConnectionString);

            this.ContainerClient = blobServiceClient.GetBlobContainerClient(settings.ContainerName);
        }

        protected StorageSettings Settings { get; }
        protected BlobContainerClient ContainerClient { get; }

        static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(60);

        static IDictionary<string, string> PartialMetadata = new Dictionary<string, string>
        {
            ["partial"] = "1"
        };

        private static string IdToString(CacheIdentity id) =>
            $"{id.Category}/{id.Hashcode}.dat";

        private static bool IsPendingOrLocked(BlobProperties properties) =>
            properties.LeaseState != LeaseState.Available ||
            properties.Metadata.TryGetValue("partial", out _);

        private async Task<ETag?> GetUnlockedETag(BlobClient blobClient, CancellationToken cancellationToken = default)
        {
            try
            {
                var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                if (IsPendingOrLocked(properties.Value))
                    return properties.Value.ETag;
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                    return ETag.All;

                throw;
            }

            return default;
        }

        public async Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);

            if (await GetUnlockedETag(blobClient, cancellationToken) is ETag etag && etag != ETag.All)
            {
                await blobClient.DeleteIfExistsAsync(
                    conditions: new BlobRequestConditions { IfMatch = etag },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task WithLockAsync(CacheIdentity id, WithLeaseDelegate callback, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);
            var lease      = new BlobLeaseClient(blobClient);

            // Get an exclusive lock on the blob
            ETag etag;
            try
            {
                // Truncate the blob or create a new one
                var response = await blobClient.UploadAsync(
                    Stream.Null,
                    metadata: PartialMetadata,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                etag = response.Value.ETag;
            }
            catch (RequestFailedException e)
            {
                // If this is locked already, do *something*

                throw;
            }

            try 
            {
                await lease.AcquireAsync(LeaseTimeout,
                    conditions: new RequestConditions { IfMatch = etag },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException e)
            {
                // If this is locked already, do *something*

                throw;
            }

            try
            {
                // Execute delegate
                await callback.Invoke(blobClient, lease.LeaseId);
            }
            finally
            {
                // Release lock
                await lease.ReleaseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<ReaderWriter> GetReadWriteHandleAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            // Check if lease or pending lease
            var path       = IdToString(id);
            var blobClient = ContainerClient.GetBlobClient(path);

            if (await GetUnlockedETag(blobClient, cancellationToken) is ETag etag && etag != ETag.All)
            {
                // File can be downloaded
                return ReaderWriter.CreateReader(blobClient, etag);
            }

            try
            {
                var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                if (IsPendingOrLocked(properties.Value) == false)
                    return ReaderWriter.CreateReader(blobClient, properties.Value.ETag);
                else
                    return await LockCreateWriter();
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                    return await LockCreateWriter();

                throw;
            }

            async Task<ReaderWriter> LockCreateWriter()
            {
                var lease = new BlobLeaseClient(blobClient);

                // Truncate the blob or create a new one
                var response = await blobClient!.UploadAsync(
                    Stream.Null,
                    metadata: PartialMetadata,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                await lease.AcquireAsync(LeaseTimeout,
                    conditions: new RequestConditions { IfMatch = response.Value.ETag },
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                try
                {
                    return ReaderWriter.CreateWriter(blobClient, response.Value.ETag);
                }
                finally
                {
                    await lease.ReleaseAsync().ConfigureAwait(false);
                }   
            }
        }
    }

    public class ReaderWriter : IAsyncDisposable
    {
        internal ReaderWriter()
        {

        }

        internal static ReaderWriter CreateReader(BlobClient blobClient, ETag etag)
        {
            return new ReaderWriter
            {

            };
        }

        public ValueTask DisposeAsync()
        {
            // Release lock (if it has one)

            return default;
        }
    }
}