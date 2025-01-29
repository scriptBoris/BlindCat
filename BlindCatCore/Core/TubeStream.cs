using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Core;

public class TubeStream : Stream
{
    private readonly Func<CryptoStream?> _remake;
    private CryptoStream _source;
    private readonly long _length;
    private long _position;

    public TubeStream(CryptoStream source, long originFileSize, Func<CryptoStream?> remake)
    {
        _source = source;
        _length = originFileSize;
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

        if (newPosition > Position)
        {   
            // fast read next
            SeekThroughRead(_source, Position, newPosition);
            _position = newPosition;
            return _position;
        }
        else if (newPosition < Position)
        {
            // remake
            _source.Dispose();
            var nev = _remake();
            if (nev == null)
            {
                throw new IOException();
            }

            SeekThroughRead(nev, 0, newPosition);
            _source = nev;
            _position = newPosition;
            return _position;
        }
        else
        {
            // do nothing
            return newPosition;
        }
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

    public override int ReadByte()
    {
        int dat = _source.ReadByte();
        _position += 1;
        return dat;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int ln = _source.Read(buffer, offset, count);
        _position += ln;
        return Task.FromResult(ln);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _source.Dispose();
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
    
    private long SeekThroughRead(Stream stream, long currentPos, long targetPosition)
    {
        const int bufferSize = 4096; // Размер буфера для пропуска данных
        var buffer = new byte[bufferSize];
        long currentPosition = currentPos;

        while (currentPosition < targetPosition)
        {
            // Сколько данных нужно пропустить
            long remaining = targetPosition - currentPosition;
            int bytesToRead = (int)Math.Min(bufferSize, remaining);

            // Читаем данные и обновляем текущую позицию
            int bytesRead = stream.Read(buffer, 0, bytesToRead);
            if (bytesRead == 0)
            {
                // Достигнут конец потока
                throw new InvalidOperationException("Cannot seek beyond the end of the stream.");
            }

            currentPosition += bytesRead;
        }

        return currentPosition; // Возвращаем новую позицию (должна совпадать с targetPosition)
    }
}