using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Azure;

namespace Wivuu.GlobalCache.AzureStorage
{
    internal class PrimedReadStream : Stream
    {
        private readonly Stream BaseStream;
        private readonly byte[] InitialBuffer;
        const int InitialReadSize = 1;
        private int? PrimedBytes;

        public PrimedReadStream(Stream baseStream)
        {
            BaseStream    = baseStream;
            InitialBuffer = ArrayPool<byte>.Shared.Rent(InitialReadSize);
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

        /// <summary>
        /// Attempt to 'prime' the pump to see if an error occurs
        /// </summary>
        public async Task<bool> TryPrimeAsync()
        {
            if (PrimedBytes > 0)
                return true;

            try
            {
                PrimedBytes = await BaseStream.ReadAsync(InitialBuffer, 0, InitialReadSize);
                return true;
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                    return false;

                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush() => BaseStream.Flush();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (PrimedBytes is int primed)
            {
                PrimedBytes = null;

                InitialBuffer[0..primed].CopyTo(buffer, 0);

                return primed + BaseStream.Read(buffer, primed, count - primed);
            }

            return BaseStream.Read(buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long Seek(long offset, SeekOrigin origin) =>
            BaseStream.Seek(offset, origin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SetLength(long value) =>
            BaseStream.SetLength(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count) =>
            BaseStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            BaseStream.Dispose();

            ArrayPool<byte>.Shared.Return(InitialBuffer);
        }
    }
}