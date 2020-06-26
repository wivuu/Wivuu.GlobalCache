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

        protected BlobContainerClient ContainerClient { get; }
        protected BlobBatchClient? BatchClient { get; }

        static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(60);

        static string IdToString(CacheId id) =>
            id.IsCategory
            ? id.ToString()
            : $"{id}.dat";

        private async Task<AsyncDisposable?> EnterWrite(string path)
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

        public async Task<T> OpenReadWriteAsync<T>(CacheId id,
                                                   Func<Stream, Task<T>>? onRead = default,
                                                   Func<Stream, Task<T>>? onWrite = default,
                                                   CancellationToken cancellationToken = default)
        {
            var path   = IdToString(id);
            var client = ContainerClient.GetBlobClient(path);

            using var retries = new RetryHelper(1, 500, totalMaxDelay: LeaseTimeout);

            // Wait for break in traffic
            do
            {
                // Try to READ file
                if (onRead != null)
                {
                    var pipe = new Pipe();

                    try
                    {
                        using var cts          = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        using var writerStream = pipe.Writer.AsStream(true);

                        var readerTask = Task.Run(() => onRead(pipe.Reader.AsStream(true)));

                        await client.DownloadToAsync(writerStream, cancellationToken: cts.Token);
                        pipe.Writer.Complete();

                        return await readerTask;
                    }
                    catch (RequestFailedException e)
                    {
                        pipe.Writer.Complete(e);

                        if (e.Status != 404)
                            throw;
                    }
                    catch (Exception e)
                    {
                        pipe.Writer.Complete(e);
                    }
                }

                // Try to WRITE file
                if (onWrite != null)
                {
                    // Create lock
                    if (!(await EnterWrite(path).ConfigureAwait(false) is IAsyncDisposable disposable))
                        // Unable to enter write
                        continue;

                    // Create a new pipe
                    var pipe = new Pipe();

                    // Upload file
                    try
                    {
                        using var cts          = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        using var readerStream = pipe.Reader.AsStream(true);

                        var writerTask = Task.Run(() => onWrite(pipe.Writer.AsStream(true)));

                        await Task.WhenAll(
                            writerTask
                                .ContinueWith(t =>
                                {
                                    pipe.Writer.Complete(t.Exception?.GetBaseException());
                                    return t.Result;
                                }),
                            client
                                .UploadAsync(readerStream, cancellationToken: cts.Token)
                                .ContinueWith(t => pipe.Reader.Complete(t.Exception?.GetBaseException()))
                        ).ConfigureAwait(false);

                        if (writerTask.IsCompletedSuccessfully)
                            return writerTask.Result;
                    }
                    finally
                    {
                        await disposable.DisposeAsync().ConfigureAwait(false);
                    }
                }

                if (await retries.DelayAsync().ConfigureAwait(false) == false)
                    throw new TimeoutException();
            }
            while (!cancellationToken.IsCancellationRequested);

            throw new TaskCanceledException();
        }
    }
}