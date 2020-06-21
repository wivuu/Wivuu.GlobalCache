using System;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
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

                        await blobClient.DownloadToAsync(writerStream).ConfigureAwait(false);
                        await pipe.Writer.CompleteAsync().ConfigureAwait(false);
                    }
                    catch (Exception error)
                    {
                        await pipe.Writer.CompleteAsync(error).ConfigureAwait(false);
                    }
                });

                return pipe.Reader.AsStream();
            }

            return Stream.Null;
        }

        public Stream OpenWrite(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
            {
                return new ReadWriteStream(stream => 
                    blobClient.UploadAsync(stream, overwrite: true, cancellationToken)
                );
            }

            throw new Exception("Failed to open write stream");
        }

        public Task RemoveAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
                return blobClient.DeleteIfExistsAsync();

            return Task.CompletedTask;
        }

        public async Task<IAsyncDisposable> TryAcquireLockAsync(CacheIdentity id, CancellationToken cancellationToken = default)
        {
            var path = IdToString(id);

            if (ContainerClient.GetBlobClient(path) is BlobClient blobClient)
            {
                var lease = new BlobLeaseClient(blobClient);

                await lease.AcquireAsync(LeaseTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);

                return new AsyncDisposable(() => lease.ReleaseAsync());
            }

            return AsyncDisposable.CompletedTask;
        }

        internal sealed class ReadWriteStream : Stream
        {
            public override bool CanRead => Reader.CanRead;
            public override bool CanSeek => Reader.CanSeek;
            public override bool CanWrite => Writer.CanWrite;
            public override bool CanTimeout => Reader.CanTimeout;
            public override long Length => Reader.Length;
            public override long Position { get => Reader.Position; set => Reader.Position = value; }
            public override int ReadTimeout { get => Reader.ReadTimeout; set => Reader.ReadTimeout = value; }
            public override int WriteTimeout { get => Reader.WriteTimeout; set => Reader.WriteTimeout = value; }

            public Pipe Pipe { get; }
            public Stream Reader { get; }
            public Stream Writer { get; }
            public Task ReaderTask { get; }

            internal ReadWriteStream(Func<Stream, Task> ReaderTask)
            {
                this.Pipe       = new Pipe();
                this.Reader     = Pipe.Reader.AsStream();
                this.Writer     = Pipe.Writer.AsStream();
                this.ReaderTask = Task.Run(() => ReaderTask(Reader));
            }

            public override void Flush() => Writer.Flush();

            public override int Read(byte[] buffer, int offset, int count) => Reader.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => Reader.Seek(offset, origin);

            public override void SetLength(long value) => Writer.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) => Writer.Write(buffer, offset, count);

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
                Reader.BeginRead(buffer, offset, count, callback, state);

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
                Writer.BeginWrite(buffer, offset, count, callback, state);

            public override void Close()
            {
                Writer.Close();
                Reader.Close();
            }

            public override void CopyTo(Stream destination, int bufferSize) =>
                Reader.CopyTo(destination, bufferSize);

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
                Reader.CopyToAsync(destination, bufferSize, cancellationToken);

            protected override void Dispose(bool disposing)
            {
                DisposeAsync().AsTask().Wait();
            }

            public override async ValueTask DisposeAsync()
            {
                await Pipe.Writer.CompleteAsync().ConfigureAwait(false);
                await ReaderTask.ConfigureAwait(false);
                await Pipe.Reader.CompleteAsync().ConfigureAwait(false);
                
                await base.DisposeAsync().ConfigureAwait(false);
            }

            public override int EndRead(IAsyncResult asyncResult) => 
                Reader.EndRead(asyncResult);

            public override void EndWrite(IAsyncResult asyncResult) =>
                Writer.EndWrite(asyncResult);

            public override Task FlushAsync(CancellationToken cancellationToken) =>
                Writer.FlushAsync(cancellationToken);

            public override int Read(Span<byte> buffer) =>
                Reader.Read(buffer);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                Reader.ReadAsync(buffer, offset, count, cancellationToken);

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                Reader.ReadAsync(buffer, cancellationToken);

            public override int ReadByte() =>
                Reader.ReadByte();

            public override void Write(ReadOnlySpan<byte> buffer) => Writer.Write(buffer);

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => 
                Writer.WriteAsync(buffer, offset, count, cancellationToken);

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => 
                Writer.WriteAsync(buffer, cancellationToken);

            public override void WriteByte(byte value) => 
                Writer.WriteByte(value);
        }

        private struct AsyncDisposable : IAsyncDisposable
        {
            public AsyncDisposable(Func<Task> done)
            {
                Done = done;
            }

            public static IAsyncDisposable CompletedTask { get; } 
                = new AsyncDisposable(() => Task.CompletedTask);

            public Func<Task> Done { get; }

            public ValueTask DisposeAsync() => new ValueTask(Done());
        }
    }
}