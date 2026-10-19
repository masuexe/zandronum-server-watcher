using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Services;

namespace ZandronumWatcher.App;

/// <summary>
/// <c>--list</c>: prints the servers reachable from a master server so the user can choose
/// which ones to pin.
/// </summary>
internal static class DiscoverRunner
{
    public static async Task<int> RunAsync(string? masterText, bool showEmpty)
    {
        var master = ServerEndpoint.TryParse(masterText ?? "", out var parsed)
            ? parsed
            : new ServerEndpoint("master.zandronum.com", 15300);

        Console.WriteLine($"Querying {master} …");

        var options = new ServerQueryOptions { MaxConcurrency = 16 };
        var directory = new ServerDirectory(new ZandronumClient(options.Timeout), options);

        // Progress every 25 servers: printing per server produces unreadable output when
        // stdout is redirected, where '\r' does not overwrite anything.
        var servers = await directory.DiscoverAsync(master, count =>
        {
            if (count > 0 && count % 25 == 0)
            {
                Console.WriteLine($"  {count} queried…");
            }
        }).ConfigureAwait(false);
        Console.WriteLine($"  {servers.Count} reachable server(s).");
        Console.WriteLine();

        var shown = servers.Where(s => showEmpty || s.HumanCount > 0).ToList();
        if (shown.Count == 0)
        {
            Console.WriteLine("Nothing to show. Use --all to include empty servers.");
            return 0;
        }

        foreach (var server in shown)
        {
            var slots = server.MaxClients > 0 ? $"{server.HumanCount}/{server.MaxClients}" : server.HumanCount.ToString();
            Console.WriteLine($"  {slots,-7} {server.Address,-22} {Truncate(server.DisplayName, 46)}");
            var where = string.Join(" · ", new[] { server.GameModeName, server.MapName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(where))
            {
                Console.WriteLine($"  {"",-7} {"",-22} {where}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Add the addresses you want to servers.json to pin them.");
        return 0;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
