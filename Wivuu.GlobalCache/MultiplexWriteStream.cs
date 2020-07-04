using System;
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

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => BaseStream.CanWrite;
        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => throw new NotSupportedException($"Cannot set Position on a {typeof(MultiplexWriteStream)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush()
        {
            BaseStream.Flush();
            OtherStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => 
            throw new NotSupportedException($"Cannot Read from a {typeof(MultiplexWriteStream)}");

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException($"Cannot Seek on a {typeof(MultiplexWriteStream)}");

        public override void SetLength(long value) => 
            throw new NotSupportedException($"Cannot SetLength on a ${typeof(MultiplexWriteStream)}");

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