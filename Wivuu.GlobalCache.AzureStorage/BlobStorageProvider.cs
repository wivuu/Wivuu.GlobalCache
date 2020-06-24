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
        public BlobStorageProvider(BlobContainerClient container)
        {
            if (container == null)
                throw new ArgumentNullException($"{nameof(container)} is required to operate");

            this.ContainerClient = container;
        }

        protected BlobContainerClient ContainerClient { get; }

        static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(60);

        static string IdToString(CacheIdentity id) =>
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

        public async Task<bool> RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            if (id.IsCategory)
            {
                // Delete all blobs in category
                var blobs = ContainerClient.GetBlobsAsync(
                    states: BlobStates.None,
                    prefix: id.ToString(),
                    cancellationToken: cancellationToken
                );

                var delete = new List<Task<bool>>();

                await foreach (var blobMeta in blobs)
                {
                    if (!blobMeta.Deleted)
                    {
                        var blob = ContainerClient.GetBlobClient(blobMeta.Name);

                        delete.Add(
                            blob.DeleteIfExistsAsync(cancellationToken: cancellationToken)
                                .ContinueWith(t => t.IsCompletedSuccessfully ? true : false)
                        );
                    }
                }

                var all = await Task.WhenAll(delete).ConfigureAwait(false);

                return delete.Count == 0 || delete.TrueForAll(t => t.Result);
            }
            else
            {
                var path   = IdToString(id);
                var client = ContainerClient.GetBlobClient(path);
                var result = await client.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                return result.Value;
            }
        }

        public async Task<T> OpenReadWriteAsync<T>(CacheIdentity id,
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

                        await Task.WhenAll(
                            readerTask
                                .ContinueWith(t => pipe.Reader.Complete(t.Exception?.GetBaseException())),
                            client
                                .DownloadToAsync(writerStream, cancellationToken: cts.Token)
                                .ContinueWith(t => pipe.Writer.Complete(t.Exception?.GetBaseException()))
                        ).ConfigureAwait(false);

                        if (readerTask.IsCompletedSuccessfully)
                            return readerTask.Result;
                    }
                    catch (RequestFailedException e)
                    {
                        // Throw if error is not 404
                        if (e.Status != 404)
                            throw;
                    }
                }

                // Try to WRITE file
                if (onWrite != null)
                {
                    // Create a new pipe
                    var pipe = new Pipe();

                    // Create lock
                    if (!(await EnterWrite(path).ConfigureAwait(false) is IAsyncDisposable disposable))
                        // Unable to enter write
                        continue;

                    // Upload file
                    try
                    {
                        using var cts          = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        using var readerStream = pipe.Reader.AsStream(true);

                        var writerTask = Task.Run(() => onWrite(pipe.Writer.AsStream(true)));

                        await Task.WhenAll(
                            writerTask
                                .ContinueWith(t => pipe.Writer.Complete(t.Exception?.GetBaseException())),
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