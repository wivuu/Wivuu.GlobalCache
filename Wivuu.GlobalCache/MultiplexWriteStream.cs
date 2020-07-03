using System.IO;
using System.Runtime.CompilerServices;

namespace Wivuu.GlobalCache
{
    internal class MultiplexWriteStream : Stream
    {
        private readonly Stream BaseStream;
        private readonly Stream OtherStream;

        public MultiplexWriteStream(Stream baseStream, Stream otherStream)
        {
            BaseStream  = baseStream;
            OtherStream = otherStream;
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
        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStream.Write(buffer, offset, count);
            OtherStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            BaseStream.Dispose();
            OtherStream.Dispose();
        }
    }
}