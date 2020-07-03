using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public sealed class StreamWithCompletion : Stream
    {
        private readonly Stream BaseStream;
        private readonly Task Completion;

        internal StreamWithCompletion(Stream baseStream, Task completion)
        {
            BaseStream = baseStream;
            Completion = completion;
        }

        public override bool CanRead => BaseStream.CanRead;
        public override bool CanSeek => BaseStream.CanSeek;
        public override bool CanWrite => BaseStream.CanWrite;
        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush() => BaseStream.Flush();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(byte[] buffer, int offset, int count) => 
            BaseStream.Read(buffer, offset, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long Seek(long offset, SeekOrigin origin) =>
            BaseStream.Seek(offset, origin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetLength(long value) =>
            BaseStream.SetLength(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count) =>
            BaseStream.Write(buffer, offset, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TaskAwaiter GetAwaiter() =>
            Completion.GetAwaiter();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(true);
            BaseStream.Dispose();
        }
    }
}