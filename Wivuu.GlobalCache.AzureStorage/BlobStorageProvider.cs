using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

[assembly: InternalsVisibleTo("Web")]

namespace Wivuu.GlobalCache.AzureStorage
{
    public class BlobStorageProvider : IStorageProvider
    {
        /// <summary>
        /// Blob storage provider which utilizes leases to coordinate concurrent readers and writers
        /// </summary>
        /// <param name="container">The blob container to store cached items in</param>
        public BlobStorageProvider(BlobContainerClient container, BlobBatchClient? batchClient = default)
        {
            if (container == null)
                throw new ArgumentNullException($"{nameof(container)} is required to operate");

            this.ContainerClient = container;
            this.BatchClient     = batchClient;
        }

        internal BlobContainerClient ContainerClient { get; }
        internal BlobBatchClient? BatchClient { get; }

        static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(60);

        static string IdToString(CacheId id) =>
            id.IsCategory
            ? id.ToString()
            : $"{id}.dat";

        internal async Task<AsyncDisposable?> EnterWrite(string path)
        {
            try
            {
                var lockFile = ContainerClient.GetBlobClient(path + ".lock");

                await lockFile
                    .UploadAsync(Stream.Null, conditions: new BlobRequestConditions { IfNoneMatch = ETag.All })
                    .ConfigureAwait(false);

                var leaseClient = new BlobLeaseClient(lockFile);

                await leaseClient.AcquireAsync(LeaseTimeout).ConfigureAwait(false);

                return new AsyncDisposable(async () =>
                {
                    await lockFile
                        .DeleteIfExistsAsync(conditions: new BlobRequestConditions { LeaseId = leaseClient.LeaseId })
                        .ConfigureAwait(false);
                });
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 409 || e.Status == 412)
                    return default;

                throw;
            }
        }

        public async Task<bool> RemoveAsync(CacheId id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            var blobs = ContainerClient.GetBlobsAsync(
                states: BlobStates.None,
                prefix: path,
                cancellationToken: cancellationToken
            );

            if (this.BatchClient is BlobBatchClient batchClient)
            {
                await using var asyncEnumerator = blobs.GetAsyncEnumerator(cancellationToken);

                var keepGoing = true;
                var removed   = 0;

                do
                {
                    using var batch = batchClient.CreateBatch();

                    while (keepGoing = await asyncEnumerator.MoveNextAsync())
                    {
                        batch.DeleteBlob(ContainerClient.Name,
                            blobName: asyncEnumerator.Current.Name,
                            snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots);

                        if (batch.RequestCount == 256)
                            break;
                    }

                    if (batch.RequestCount > 0)
                    {
                        var result = await batchClient
                            .SubmitBatchAsync(batch,
                                throwOnAnyFailure: false,
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);

                        removed += batch.RequestCount;
                    }
                }
                while (keepGoing);

                return removed != 0;
            }
            else
            {
                var delete = new List<Task<bool>>();

                await foreach (var blobMeta in blobs)
                {
                    if (!blobMeta.Deleted)
                    {
                        delete.Add(
                            ContainerClient
                                .DeleteBlobIfExistsAsync(blobMeta.Name,
                                    snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                                    cancellationToken: cancellationToken)
                                .ContinueWith(t => t.IsCompletedSuccessfully ? true : false)
                        );
                    }
                }

                var all = await Task.WhenAll(delete).ConfigureAwait(false);

                return delete.Count == 0 || delete.TrueForAll(t => t.Result);
            }
        }

        public async Task<Stream?> TryOpenRead(CacheId id, CancellationToken cancellationToken = default)
        {
            var path   = IdToString(id);
            var client = ContainerClient.GetBlobClient(path);
            var pipe   = new Pipe();

            // Read from blob storage
            _ = Task.Run(async () =>
            {
                try
                {
                    await client
                        .DownloadToAsync(pipe.Writer.AsStream(leaveOpen: true), cancellationToken)
                        .ConfigureAwait(false);

                    pipe.Writer.Complete();
                }
                catch (Exception e)
                {
                    pipe.Writer.Complete(e);
                }
            });

            // Create stream to buffer initial
            var primed = new PrimedReadStream(pipe.Reader.AsStream(leaveOpen: false));

            if (await primed.TryPrimeAsync())
                return primed;

            else
                primed.Dispose();

            return null;
        }

        public async Task<StreamWithCompletion?> TryOpenWrite(CacheId id, CancellationToken cancellationToken = default)
        {
            var path   = IdToString(id);
            var client = ContainerClient.GetBlobClient(path);
            var pipe   = new Pipe();
            
            if (await EnterWrite(path) is IAsyncDisposable lease)
            {
                // Write to blob storage
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await client
                            .UploadAsync(pipe.Reader.AsStream(true),
                                conditions: new BlobRequestConditions { },
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        pipe.Reader.Complete(e);
                        throw;
                    }
                    finally
                    {
                        await lease.DisposeAsync();
                        await WaitForFile(path);
                    }
                });

                return new StreamWithCompletion(
                    pipe.Writer.AsStream(leaveOpen: false), 
                    task); 
            }

            return null;
        }

        /// <summary>
        /// Wait for file to exist
        /// </summary>
        private async Task WaitForFile(string path, CancellationToken cancellationToken = default)
        {
            var retries = new RetryHelper(1, 200, totalMaxDelay: TimeSpan.FromSeconds(1));
            var blob    = this.ContainerClient.GetBlobClient(path);

            while (
                !await blob.ExistsAsync(cancellationToken).ConfigureAwait(false) &&
                !cancellationToken.IsCancellationRequested &&
                await retries.DelayAsync().ConfigureAwait(false)
            );
        }
    }
}