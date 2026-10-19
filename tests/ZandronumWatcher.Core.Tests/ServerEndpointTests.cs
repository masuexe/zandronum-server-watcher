using ZandronumWatcher.Core.Models;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

public class ServerEndpointTests
{
    [Theory]
    [InlineData("example.org:10666", "example.org", 10666)]
    [InlineData(" 1.2.3.4:1234 ", "1.2.3.4", 1234)]
    [InlineData("[::1]:10666", "::1", 10666)]
    public void Parses_valid_endpoints(string text, string host, int port)
    {
        Assert.True(ServerEndpoint.TryParse(text, out var endpoint));
        Assert.Equal(host, endpoint.Host);
        Assert.Equal(port, endpoint.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nohost")]
    [InlineData("host:")]
    [InlineData("host:0")]
    [InlineData("host:70000")]
    [InlineData(":10666")]
    public void Rejects_invalid_endpoints(string text)
    {
        Assert.False(ServerEndpoint.TryParse(text, out _));
    }

    [Fact]
    public void Round_trips_through_ToString()
    {
        var endpoint = ServerEndpoint.Parse("example.org:10666");

        Assert.Equal("example.org:10666", endpoint.ToString());
    }
}
