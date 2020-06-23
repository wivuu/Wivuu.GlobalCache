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

namespace Wivuu.GlobalCache.AzureStorage
{
    public class BlobStorageProvider4 : IStorageProvider
    {
        public BlobStorageProvider4(StorageSettings settings)
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
        
        public async Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path   = IdToString(id);
            var client = ContainerClient.GetBlobClient(path);

            // TODO: Should we check for a lock?

            await client.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<IStorageReaderWriter> OpenReadWriteAsync(CacheIdentity id, [EnumeratorCancellation]CancellationToken cancellationToken = default)
        {
            var path   = IdToString(id);
            var client = ContainerClient.GetBlobClient(path);

            using var retries = new RetryHelper(1, 30, totalMaxDelay: LeaseTimeout);

            // Wait for break in traffic
            do
            {
                // Try to READ file
                {
                    var pipe = new Pipe();

                    using var cts          = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    using var writerStream = pipe.Writer.AsStream(true);
                    
                    var downloadTask = client
                        .DownloadToAsync(writerStream, cancellationToken: cts.Token)
                        .ContinueWith(t => 
                        {
                            if (t.Exception?.GetBaseException() is RequestFailedException e && e.Status == 404)
                                pipe.Writer.Complete(new NoCacheEntryException());
                            else
                                pipe.Writer.Complete(t.Exception?.GetBaseException());
                        });

                    yield return new StorageReader(pipe.Reader.AsStream());
                }

                // Try to WRITE file
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

                        var uploadTask = client
                            .UploadAsync(readerStream, cancellationToken: cts.Token)
                            .ContinueWith(t => pipe.Reader.Complete(t.Exception?.GetBaseException()));

                        yield return new StorageWriter(pipe.Writer.AsStream());
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

    public class StorageWriter : IStorageReaderWriter
    {
        internal StorageWriter(Stream stream)
        {
            this.Stream = stream;
        }

        public Stream Stream { get; }
    }

    public class StorageReader : IStorageReaderWriter
    {
        internal StorageReader(Stream stream)
        {
            this.Stream = stream;
        }

        public Stream Stream { get; }
    }

    public interface IStorageReaderWriter
    {

    }

    public class NoCacheEntryException : Exception
    {
        
    }
}