using ZandronumWatcher.Core.Configuration;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.App;

/// <summary>
/// <c>--pin host:port</c> / <c>--unpin host:port</c>: edit the pinned list without making the
/// user hand-edit JSON.
/// </summary>
internal static class PinRunner
{
    public static int Run(string configPath, IReadOnlyList<string> addresses, bool remove)
    {
        var config = WatchConfig.Load(configPath);
        var changed = 0;

        foreach (var text in addresses)
        {
            if (!ServerEndpoint.TryParse(text, out var endpoint))
            {
                Console.Error.WriteLine($"skipped '{text}': not a host:port");
                continue;
            }

            var pinnedNow = PinList.Contains(config.Servers, endpoint);

            if (remove)
            {
                if (PinList.Unpin(config.Servers, endpoint))
                {
                    changed++;
                    Console.WriteLine($"unpinned {endpoint}");
                }
                else
                {
                    Console.WriteLine($"{endpoint} was not pinned");
                }
            }
            else if (pinnedNow)
            {
                Console.WriteLine($"{endpoint} is already pinned");
            }
            else
            {
                PinList.Pin(config.Servers, endpoint);
                changed++;
                Console.WriteLine($"pinned {endpoint}");
            }
        }

        if (changed > 0)
        {
            config.Save(configPath);
            Console.WriteLine($"saved {configPath}");
        }

        return 0;
    }
}