using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Core
{
    public class TubeStream : Stream
    {
        private readonly Func<long, AppResponse<CryptoStream>> _remake;
        private CryptoStream _source;
        private readonly long _length;
        private long _position;

        public TubeStream(CryptoStream source, long dataLength, Func<long, AppResponse<CryptoStream>> remake)
        {
            _source = source;
            _length = dataLength;
            _remake = remake;
        }

        public override bool CanRead => _source.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            _source.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _source.FlushAsync();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = Position + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = Length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), "Invalid SeekOrigin value.");
            }

            if (newPosition == Position)
                return newPosition;

            _source.Dispose();
            var nev = _remake(newPosition);
            if (nev.IsFault && !nev.IsCanceled)
            {
                throw new IOException(nev.Description, nev.Exception);
            }

            _source = nev.Result;
            _position = newPosition;
            return _position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_source.CanRead)
                return 0;

            try
            {
                int ln = _source.Read(buffer, offset, count);
                _position += ln;
                return ln;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public override int Read(Span<byte> buffer)
        {
            return base.Read(buffer);
        }

        public override int ReadByte()
        {
            int dat = _source.ReadByte();
            _position += 1;
            return dat;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return base.BeginRead(buffer, offset, count, callback, state);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int ln = _source.Read(buffer, offset, count);
            _position += ln;
            return Task.FromResult(ln);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(buffer, cancellationToken);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return base.EndRead(asyncResult);
        }

        //public override void CopyTo(Stream destination, int bufferSize)
        //{
        //    base.CopyTo(destination, bufferSize);
        //}

        //public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        //{
        //    return base.CopyToAsync(destination, bufferSize, cancellationToken);
        //}

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source?.Dispose();
            }
            base.Dispose(disposing);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
