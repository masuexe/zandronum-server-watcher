using System.Text.Json;
using ZandronumWatcher.Core.Protocol;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

public class HuffmanTests
{
    public static TheoryData<string, string> Vectors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "huffman_vectors.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var data = new TheoryData<string, string>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            data.Add(item.GetProperty("raw_hex").GetString()!, item.GetProperty("encoded_hex").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Encodes_to_reference_bytes(string rawHex, string encodedHex)
    {
        var raw = Convert.FromHexString(rawHex);
        var expected = Convert.FromHexString(encodedHex);

        Assert.Equal(expected, Huffman.Encode(raw));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Decodes_reference_bytes(string rawHex, string encodedHex)
    {
        var expected = Convert.FromHexString(rawHex);
        var encoded = Convert.FromHexString(encodedHex);

        Assert.Equal(expected, Huffman.Decode(encoded));
    }

    [Fact]
    public void Round_trips_all_byte_values()
    {
        var payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        Assert.Equal(payload, Huffman.Decode(Huffman.Encode(payload)));
    }

    [Fact]
    public void Raw_marker_returns_payload_untouched()
    {
        byte[] data = [0xFF, 0x01, 0x02, 0x03];

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, Huffman.Decode(data));
    }
}
