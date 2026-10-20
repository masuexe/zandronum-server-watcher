using System.IO;
using ZandronumWatcher.Core.Configuration;
using ZandronumWatcher.Core.Import;

namespace ZandronumWatcher.App;

/// <summary>
/// <c>--import-doomseeker [ini path] [--dry-run]</c>: copies Doomseeker's custom-server list
/// into the pinned list.
/// </summary>
internal static class DoomseekerImportRunner
{
    public static int Run(string configPath, string? iniPath, bool dryRun)
    {
        iniPath ??= DoomseekerImporter.FindConfig();
        if (iniPath is null || !File.Exists(iniPath))
        {
            Console.Error.WriteLine("Could not find doomseeker.ini. Looked in:");
            foreach (var candidate in DoomseekerImporter.CandidatePaths)
            {
                Console.Error.WriteLine("  " + candidate);
            }

            Console.Error.WriteLine("Pass the path explicitly: --import-doomseeker <path-to-doomseeker.ini>");
            return 1;
        }

        Console.WriteLine("reading " + iniPath);

        DoomseekerImportResult result;
        try
        {
            result = DoomseekerImporter.Parse(File.ReadAllText(iniPath));
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"could not read {iniPath}: {ex.Message}");
            return 1;
        }

        if (result.SkippedEngines.Count > 0)
        {
            Console.WriteLine(
                "skipped other engines (this watcher only speaks Zandronum): " +
                string.Join(", ", result.SkippedEngines));
        }

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine($"ignored {result.Warnings.Count} malformed entr{(result.Warnings.Count == 1 ? "y" : "ies")}");
        }

        if (result.Servers.Count == 0)
        {
            Console.WriteLine("no Zandronum servers found in that file");
            return 0;
        }

        var config = WatchConfig.Load(configPath);
        var added = 0;
        var already = 0;
        var verb = dryRun ? "would pin" : "pinned";

        foreach (var server in result.Servers)
        {
            if (PinList.Contains(config.Servers, server))
            {
                already++;
                Console.WriteLine($"  {"already pinned",-12} {server}");
                continue;
            }

            if (!dryRun)
            {
                PinList.Pin(config.Servers, server);
            }

            added++;
            Console.WriteLine($"  {verb,-12} {server}");
        }

        if (dryRun)
        {
            Console.WriteLine($"\ndry run: {added} to add, {already} already pinned");
            Console.WriteLine("re-run without --dry-run to apply");
            return 0;
        }

        if (added > 0)
        {
            config.Save(configPath);
            Console.WriteLine($"\nsaved {configPath} (+{added}, {already} already pinned)");
        }
        else
        {
            Console.WriteLine($"\nnothing to do: all {already} server(s) already pinned");
        }

        return 0;
    }
}
