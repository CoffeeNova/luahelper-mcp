using System.Text;

namespace LuaHelperMcpServer.Tests.Unit.Helpers;

/// <summary>
/// A stream that writes data in two chunks to simulate partial reads.
/// </summary>
internal sealed class PartialWriteStream : Stream
{
    private readonly byte[] _first;
    private readonly byte[] _second;
    private int _position;
    private bool _firstReturned;

    public PartialWriteStream(string first, string second)
    {
        _first = Encoding.UTF8.GetBytes(first);
        _second = Encoding.UTF8.GetBytes(second);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _first.Length + _second.Length;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _first.Length + _second.Length)
            return 0;

        byte[] source;
        int sourceOffset;
        int available;

        if (!_firstReturned)
        {
            source = _first;
            sourceOffset = Math.Min(_position, _first.Length);
            available = _first.Length - sourceOffset;
            _firstReturned = true;
        }
        else
        {
            var secondOffset = Math.Max(0, _position - _first.Length);
            source = _second;
            sourceOffset = secondOffset;
            available = _second.Length - secondOffset;
        }

        var toCopy = Math.Min(count, available);
        Array.Copy(source, sourceOffset, buffer, offset, toCopy);
        _position += toCopy;
        return toCopy;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(Read(buffer, offset, count));
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override void Flush() { }
}
