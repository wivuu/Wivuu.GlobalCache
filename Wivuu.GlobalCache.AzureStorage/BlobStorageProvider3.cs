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
    public class BlobStorageProvider3 : IStorageProvider
    {
        public BlobStorageProvider3(StorageSettings settings)
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

        private async Task<bool> WaitForLock(BlobClient lockClient, string path) 
        {
            using var retry = new RetryHelper(1, 50, totalMaxDelay: TimeSpan.FromSeconds(60));

            do
            {
                if (await lockClient.ExistsAsync() == false)
                    return true;

                if (await retry.DelayAsync() == false)
                    return false;
            }
            while (true);
        }
        
        private async Task<bool> CreateLock(BlobClient lockClient, string path)
        {
            try
            {
                await lockClient.UploadAsync(Stream.Null, conditions: new BlobRequestConditions { IfNoneMatch = ETag.All });

                return true;
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 409)
                    return false;

                throw;
            }
        }
        
        public async Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var client     = ContainerClient.GetBlobClient(path);
            var lockClient = ContainerClient.GetBlobClient(path + ".lock");

            // If lockfile exists - WAIT FOR LOCKFILE to go away (timeout 1 minute)
            if (!await WaitForLock(lockClient, path))
                throw new Exception($"{path} was locked");

            await client.DeleteIfExistsAsync();
        }

        public async Task<T> OpenReadWriteAsync<T>(CacheIdentity id, ReaderWriterHandle<T> handle, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var client     = ContainerClient.GetBlobClient(path);
            var lockClient = ContainerClient.GetBlobClient(path + ".lock");

            using var retries = new RetryHelper(1, 30);

            // Wait for break in traffic
            do
            {
                // If lockfile exists - WAIT FOR LOCKFILE to go away (timeout 1 minute)
                if (!await WaitForLock(lockClient, path))
                    throw new Exception($"{path} was locked");

                // Try to READ file
                if (handle.Reader is ReaderWriterHandle<T>.ReadCallback reader)
                {
                    var pipe = new Pipe();

                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        using var writerStream = pipe.Writer.AsStream(true);

                        var readerTask = reader(pipe.Reader.AsStream());
                        
                        await Task.WhenAll(
                            readerTask.ContinueWith(t => pipe.Reader.Complete(t.Exception?.GetBaseException())),
                            client.DownloadToAsync(writerStream, cancellationToken: cts.Token).ContinueWith(t => pipe.Writer.Complete(t.Exception?.GetBaseException()))
                        );

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
                if (handle.Writer is ReaderWriterHandle<T>.WriteCallback writer)
                {
                    // Create a new pipe
                    var pipe = new Pipe();

                    // Create lock
                    if (await CreateLock(lockClient, path) == false)
                        // Skip to next iteration of loop
                        continue;

                    // Upload file
                    try
                    {
                        using var readerStream = pipe.Reader.AsStream();

                        var writerTask = writer(pipe.Writer.AsStream());
                        
                        await Task.WhenAll(
                            client.UploadAsync(readerStream, cancellationToken: cancellationToken).ContinueWith(t => pipe.Reader.Complete(t.Exception?.GetBaseException())),
                            writerTask.ContinueWith(t => pipe.Writer.Complete(t.Exception?.GetBaseException()))
                        ).ConfigureAwait(false);

                        if (writerTask.IsCompletedSuccessfully)
                            return writerTask.Result;
                    }
                    finally
                    {
                        // Clear lock
                        await lockClient.DeleteIfExistsAsync();
                    }
                }
            }
            while (!cancellationToken.IsCancellationRequested && await retries.DelayAsync());

            throw new Exception("Failed to read write stuff");
        }
    }

    public class ReaderWriterHandle<TResult>
    {
        internal ReadCallback? Reader { get; private set; }
        internal WriteCallback? Writer { get; private set; }

        public delegate Task<TResult> ReadCallback(Stream stream);
        public delegate Task<TResult> WriteCallback(Stream stream);

        public ReaderWriterHandle<TResult> OnRead(ReadCallback reader)
        {
            this.Reader = reader;
            return this;
        }

        public ReaderWriterHandle<TResult> OnWrite(WriteCallback writer)
        {
            this.Writer = writer;
            return this;
        }
    }
}