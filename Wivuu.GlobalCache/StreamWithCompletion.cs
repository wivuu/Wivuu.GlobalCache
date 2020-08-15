using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public sealed class StreamWithCompletion : Stream
    {
        private readonly Stream _baseStream;
        private readonly Task _completion;

        internal StreamWithCompletion(Stream baseStream, Task completion)
        {
            _baseStream = baseStream;
            _completion = completion;
        }

        /// <summary>
        /// Task awaiting completion before stream is complete
        /// </summary>
        public Task Completion => _completion;

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush() => _baseStream.Flush();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(byte[] buffer, int offset, int count) => 
            _baseStream.Read(buffer, offset, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long Seek(long offset, SeekOrigin origin) =>
            _baseStream.Seek(offset, origin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetLength(long value) =>
            _baseStream.SetLength(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count) =>
            _baseStream.Write(buffer, offset, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TaskAwaiter GetAwaiter() =>
            _completion.GetAwaiter();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(true);
            _baseStream.Dispose();
        }
    }
}