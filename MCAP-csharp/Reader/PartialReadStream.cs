using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MCAP_csharp.Reader
{
    public class PartialReadStream : Stream
    {
        public static PartialReadStream From(Stream stream, long offset, long count, bool disposeBaseStreamOnClose)
        {
            if (offset < 0)
                throw new ArgumentException($"PartialStream Start must be > 0 (currently {offset})");
            if (count < 0)
                throw new ArgumentException($"PartialStream End must be > 0 (currently {count})");
            if (offset + count > stream.Length)
                throw new ArgumentException(
                    $"PartialStream offset + count must be <= to stream length (offset + count = {(offset + count)}, stream length: {stream.Length}");
            var ret = new PartialReadStream(stream, offset, count, disposeBaseStreamOnClose);
            ret.Position = 0;
            return ret;
        }

        private readonly Stream _stream;
        private readonly long _offset;
        private readonly long _count;
        private readonly bool _disposeOnClose;
        private PartialReadStream(Stream sourceStream, long offset, long count, bool disposeBaseStreamOnClose)
        {
            _stream = sourceStream;
            _offset = offset;
            _count = count;
            _disposeOnClose = disposeBaseStreamOnClose;
        }


        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => Math.Min(_stream.Length, _count);
        public long OriginalPosition => _stream.Position;
        public override long Position
        {
            get => _stream.Position - _offset;
            set => _stream.Position = value + _offset;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var actualCount = Math.Min(count, _count - Position);
            if (actualCount <= 0)
                return 0;
            return _stream.Read(buffer, offset, (int)actualCount);
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            Position = 0;
            var buffer = new byte[bufferSize];
            int read = 0;
            while ((read = this.Read(buffer, 0, buffer.Length)) > 0)
                destination.Write(buffer, 0, read);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long o;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    o = _offset + offset;
                    break;
                case SeekOrigin.Current:
                    o = offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }

            return _stream.Seek(_offset + offset, origin);
        }
        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Flush() => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposeOnClose)
                _stream.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposeOnClose)
                await _stream.DisposeAsync();
        }
    }
}
