using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ModTogetherUniversal.Services
{
    public class TrackingStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly Action<long> _onBytesTransferred;

        public TrackingStream(Stream baseStream, Action<long> onBytesTransferred)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _onBytesTransferred = onBytesTransferred ?? throw new ArgumentNullException(nameof(onBytesTransferred));
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _baseStream.Read(buffer, offset, count);
            if (read > 0) _onBytesTransferred(read);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int read = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            if (read > 0) _onBytesTransferred(read);
            return read;
        }
        
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _baseStream.ReadAsync(buffer, cancellationToken);
            if (read > 0) _onBytesTransferred(read);
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _baseStream.Write(buffer, offset, count);
            _onBytesTransferred(count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            _onBytesTransferred(count);
        }
        
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _baseStream.WriteAsync(buffer, cancellationToken);
            _onBytesTransferred(buffer.Length);
        }

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
