using System.Text;

namespace ZandronumWatcher.Core.Protocol;

/// <summary>
/// The Huffman codec used by Doom and Zandronum for all launcher/master traffic.
/// </summary>
/// <remarks>
/// Bit packing is LSB-first within each byte. The first byte of an encoded buffer stores the
/// number of padding bits appended to the final byte; a value of <c>0xFF</c> means the remainder
/// of the buffer is stored raw (this is how Zandronum escapes already-compressed payloads).
/// </remarks>
public static class Huffman
{
    private const byte RawMarker = 0xFF;
    private const int MinBits = 3;
    private const int MaxBits = 10;

    private static readonly Dictionary<string, byte> DecodeMap = BuildDecodeMap();

    private static Dictionary<string, byte> BuildDecodeMap()
    {
        var map = new Dictionary<string, byte>(256, StringComparer.Ordinal);
        for (var i = 0; i < 256; i++)
        {
            map[HuffmanTables.Encode[i]] = (byte)i;
        }

        return map;
    }

    /// <summary>Encodes a payload using the Doom Huffman table.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> data)
    {
        var bits = new StringBuilder(data.Length * 10);
        foreach (var b in data)
        {
            bits.Append(HuffmanTables.Encode[b]);
        }

        // Mirrors the reference implementation: a payload whose bit length is a multiple of 8
        // gets a full padding byte of 8 rather than 0.
        var padding = 8 - (bits.Length % 8);
        bits.Append('0', padding);

        var result = new byte[1 + (bits.Length / 8)];
        result[0] = (byte)padding;
        for (var i = 0; i < bits.Length; i += 8)
        {
            result[1 + (i / 8)] = PackBits(bits, i);
        }

        return result;
    }

    /// <summary>Decodes a Doom Huffman payload. A leading <c>0xFF</c> marks raw (unencoded) data.</summary>
    public static byte[] Decode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        var padding = data[0];
        var payload = data[1..];
        if (padding == RawMarker)
        {
            return payload.ToArray();
        }

        var totalBits = (payload.Length * 8) - padding;
        if (totalBits < 0)
        {
            throw new InvalidDataException($"Invalid Huffman padding: {padding} for {payload.Length} byte(s).");
        }

        var bits = new StringBuilder(payload.Length * 8);
        foreach (var b in payload)
        {
            for (var i = 0; i < 8; i++)
            {
                bits.Append(((b >> i) & 1) == 1 ? '1' : '0');
            }
        }

        var output = new List<byte>(totalBits / 8);
        var index = 0;
        while (index < totalBits)
        {
            var matched = false;
            for (var length = MinBits; length <= MaxBits && index + length <= totalBits; length++)
            {
                if (DecodeMap.TryGetValue(bits.ToString(index, length), out var value))
                {
                    output.Add(value);
                    index += length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                throw new InvalidDataException($"No Huffman code matched at bit offset {index}.");
            }
        }

        return output.ToArray();
    }

    private static byte PackBits(StringBuilder bits, int start)
    {
        var value = 0;
        for (var i = 0; i < 8; i++)
        {
            if (bits[start + i] == '1')
            {
                value |= 1 << i;
            }
        }

        return (byte)value;
    }
}
