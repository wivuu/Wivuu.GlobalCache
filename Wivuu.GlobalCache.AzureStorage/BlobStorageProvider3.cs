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

        public async Task OpenReadWriteAsync(CacheIdentity id, ReaderWriterHandle handle, CancellationToken cancellationToken = default)
        {
            var path       = IdToString(id);
            var client     = ContainerClient.GetBlobClient(path);
            var lockClient = ContainerClient.GetBlobClient(path + ".lock");
            var pipe       = new Pipe();

            // Wait for break in traffic
            do
            {
                // If lockfile exists - WAIT FOR LOCKFILE to go away (timeout 1 minute)
                if (!await WaitForLock(lockClient, path))
                    throw new Exception($"{path} was locked");

                // Try to READ file
                if (handle.Reader is ReaderWriterHandle.ReadCallback reader)
                {
                    try
                    {
                        using var writerStream = pipe.Writer.AsStream(true);
                        
                        var readerTask = reader(pipe.Reader.AsStream()).ContinueWith(t => pipe.Reader.Complete(t.Exception));

                        await client.DownloadToAsync(writerStream, cancellationToken: cancellationToken).ConfigureAwait(false);

                        pipe.Writer.Complete();

                        await readerTask;
                    }
                    catch (RequestFailedException e)
                    {
                        // Throw if error is not 404
                        if (e.Status != 404)
                            throw;
                    }
                }

                // Try to WRITE file
                if (handle.Writer is ReaderWriterHandle.WriteCallback writer)
                {
                    // Create lock
                    if (await CreateLock(lockClient, path) == false)
                        // Skip to next iteration of loop
                        continue;

                    // Upload file
                    try
                    {
                        using var readerStream = pipe.Reader.AsStream();

                        var writerTask = writer(pipe.Writer.AsStream()).ContinueWith(t => pipe.Writer.Complete(t.Exception));
                        
                        await Task.WhenAll(
                            client.UploadAsync(readerStream, cancellationToken: cancellationToken),
                            writerTask
                        ).ConfigureAwait(false);
                    }
                    finally
                    {
                        // Clear lock
                        await lockClient.DeleteIfExistsAsync();
                    }
                }
            }
            while (!cancellationToken.IsCancellationRequested); // TODO: replace with retry helper
        }
    }

    public class ReaderWriterHandle
    {
        internal ReadCallback? Reader { get; private set; }
        internal WriteCallback? Writer { get; private set; }

        public delegate Task ReadCallback(Stream stream);
        public delegate Task WriteCallback(Stream stream);

        public ReaderWriterHandle OnRead(ReadCallback reader)
        {
            this.Reader = reader;
            return this;
        }

        public ReaderWriterHandle OnWrite(WriteCallback writer)
        {
            this.Writer = writer;
            return this;
        }
    }
}