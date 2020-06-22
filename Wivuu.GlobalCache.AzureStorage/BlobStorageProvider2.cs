using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
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

        private static string IdToString(CacheIdentity id) =>
            $"{id.Category}/{id.Hashcode}.dat";

        internal static bool IsPendingOrLocked(BlobProperties properties) =>
            properties.LeaseState == LeaseState.Leased ||
            properties.Metadata.TryGetValue("partial", out _);

        internal static async Task<ETag?> GetUnlockedETag(BlobClient client, CancellationToken cancellationToken = default)
        {
            try
            {
                var properties = await client.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

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

            if (await GetUnlockedETag(blobClient, cancellationToken) is var etag && (etag == null || etag != ETag.All))
            {
                try
                {
                    await blobClient.DeleteIfExistsAsync(
                        conditions: new BlobRequestConditions { IfMatch = etag },
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                }
                catch (RequestFailedException e)
                {
                    if (e.Status != 412)
                        throw;
                }
            }
        }

        public async Task<ReaderWriter> CreateReaderWriterHandle(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path   = IdToString(id);
            var handle = new ReaderWriter(ContainerClient.GetBlockBlobClient(path));

            while (!cancellationToken.IsCancellationRequested &&
                   !await handle.TryOpenRead() &&
                   !await handle.TryOpenWrite())
                // await Task.Yield();
                await Task.Delay(1);

            return handle;
        }
    }

    public class ReaderWriter : IAsyncDisposable
    {
        static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(60);

        static IDictionary<string, string> PartialMetadata = new Dictionary<string, string>
        {
            ["partial"] = "1"
        };

        internal ReaderWriter(BlockBlobClient client)
        {
            this.Client = client;
            this.Pipe   = new Pipe();
        }

        protected BlockBlobClient Client { get; }
        protected Pipe Pipe { get; }
        protected bool IsReader { get; set; }
        public Task? UploadTask { get; private set; }

        public PipeWriter? Writer => IsReader ? null : Pipe.Writer;
        public PipeReader? Reader => IsReader ? Pipe.Reader : null;

        private static bool IsPendingOrLocked(BlobProperties properties) =>
            properties.LeaseState != LeaseState.Available ||
            properties.Metadata.TryGetValue("partial", out _);

        private async Task<ETag?> GetUnlockedETag(CancellationToken cancellationToken = default)
        {
            try
            {
                var properties = await Client.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!IsPendingOrLocked(properties.Value))
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

        internal async Task<bool> TryOpenRead(CancellationToken cancellationToken = default)
        {
            if (await GetUnlockedETag() is ETag etag && etag != ETag.All)
            {
                IsReader = true;
                using var writerStream = Pipe.Writer.AsStream(true);

                _ = Task.Run(async () =>
                {
                    int tries = 0;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await Client.DownloadToAsync(writerStream,
                                conditions: new BlobRequestConditions { IfMatch = etag },
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            Pipe.Writer.Complete();
                            break;
                        }
                        catch (Exception error)
                        {
                            if (tries > 5)
                            {
                                Pipe.Writer.Complete(error);
                                break;
                            }
                            else
                            {
                                await Task.Yield();
                                if (await GetUnlockedETag() is ETag newEtag && newEtag != ETag.All)
                                    etag = newEtag;
                            }
                        }
                    }
                });

                return true;
            }

            return false;
        }

        internal async Task<bool> TryOpenWrite(CancellationToken cancellationToken = default)
        {
            var lease = new BlobLeaseClient(Client);

            // Get an exclusive lock on the blob
            ETag etag;
            try
            {
                // Truncate the blob or create a new one
                var response = await Client.UploadAsync(
                    Stream.Null,
                    conditions: new BlobRequestConditions { IfNoneMatch = ETag.All },
                    metadata: PartialMetadata,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                etag = response.Value.ETag;
            }
            catch (RequestFailedException)
            {
                // If this is locked already, do *something*
                return false;
            }

            try
            {
                await lease.AcquireAsync(LeaseTimeout,
                    conditions: new RequestConditions { IfNoneMatch = ETag.All },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException)
            {
                // If this is locked already, do *something*
                return false;
            }

            IsReader = false;

            // Open writer
            UploadTask = Task.Run(async () =>
            {
                try
                {
                    using (var reader = Pipe.Reader.AsStream())
                    {
                        await Client.UploadAsync(reader,
                            conditions: new BlobRequestConditions {  IfMatch = ETag.All, LeaseId = lease.LeaseId },
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    Pipe.Reader.Complete(e);
                }
                finally
                {
                    // Release lock
                    await lease.ReleaseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            });

            return true;
        }

        public async ValueTask DisposeAsync()
        {
            // Release lock (if it has one)
            if (UploadTask is Task t)
            {
                Pipe.Writer.Complete();
                await t.ConfigureAwait(false);
            }
        }
    }
}