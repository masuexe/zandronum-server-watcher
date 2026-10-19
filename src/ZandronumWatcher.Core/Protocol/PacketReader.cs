using System.Buffers.Binary;
using System.Text;

namespace ZandronumWatcher.Core.Protocol;

/// <summary>Little-endian reader for launcher protocol payloads.</summary>
internal ref struct PacketReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _offset;

    public PacketReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _offset = 0;
    }

    public int Remaining => _data.Length - _offset;

    public byte ReadByte()
    {
        Ensure(1);
        return _data[_offset++];
    }

    public short ReadInt16()
    {
        Ensure(2);
        var value = BinaryPrimitives.ReadInt16LittleEndian(_data[_offset..]);
        _offset += 2;
        return value;
    }

    public ushort ReadUInt16()
    {
        Ensure(2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data[_offset..]);
        _offset += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        Ensure(4);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data[_offset..]);
        _offset += 4;
        return value;
    }

    public float ReadSingle()
    {
        Ensure(4);
        var value = BinaryPrimitives.ReadSingleLittleEndian(_data[_offset..]);
        _offset += 4;
        return value;
    }

    /// <summary>Reads a NUL-terminated UTF-8 string.</summary>
    public string ReadString()
    {
        var start = _offset;
        while (_offset < _data.Length && _data[_offset] != 0)
        {
            _offset++;
        }

        var value = Encoding.UTF8.GetString(_data[start.._offset]);
        if (_offset < _data.Length)
        {
            _offset++; // consume the terminator
        }

        return value;
    }

    private void Ensure(int count)
    {
        if (_offset + count > _data.Length)
        {
            throw new InvalidDataException(
                $"Truncated launcher packet: needed {count} byte(s) at offset {_offset}, {Remaining} available.");
        }
    }
}
