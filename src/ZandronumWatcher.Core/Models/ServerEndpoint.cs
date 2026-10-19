using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace ZandronumWatcher.Core.Models;

/// <summary>An <c>host:port</c> pair identifying a Zandronum server.</summary>
public readonly record struct ServerEndpoint(string Host, int Port)
{
    public override string ToString() => $"{Host}:{Port}";

    public static bool TryParse(string? text, out ServerEndpoint endpoint)
    {
        endpoint = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        var separator = text.LastIndexOf(':');
        if (separator <= 0 || separator == text.Length - 1)
        {
            return false;
        }

        var host = text[..separator].Trim();
        if (!int.TryParse(text[(separator + 1)..].Trim(), out var port) || port is <= 0 or > 65535)
        {
            return false;
        }

        // Accept bracketed IPv6 ([::1]:10666) as well as bare hosts.
        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            host = host[1..^1];
        }

        if (host.Length == 0)
        {
            return false;
        }

        endpoint = new ServerEndpoint(host, port);
        return true;
    }

    public static ServerEndpoint Parse(string text) =>
        TryParse(text, out var endpoint)
            ? endpoint
            : throw new FormatException($"'{text}' is not a valid host:port endpoint.");
}
