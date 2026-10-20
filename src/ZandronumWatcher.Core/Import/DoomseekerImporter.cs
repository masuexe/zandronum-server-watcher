using System.Text.RegularExpressions;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Import;

/// <summary>Outcome of reading a Doomseeker configuration.</summary>
public sealed record DoomseekerImportResult
{
    /// <summary>Zandronum endpoints found in Doomseeker's custom-server list.</summary>
    public IReadOnlyList<ServerEndpoint> Servers { get; init; } = [];

    /// <summary>Engines that were skipped because this watcher only speaks Zandronum.</summary>
    public IReadOnlyList<string> SkippedEngines { get; init; } = [];

    /// <summary>Entries that looked like servers but could not be used.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Reads the manually added servers from a Doomseeker <c>doomseeker.ini</c>.
/// </summary>
/// <remarks>
/// Doomseeker calls these "custom servers" and stores them as Qt serialised strings:
/// <code>CustomServers2="(Zandronum;1.2.3.4;10666;1);(Zandronum;1.2.3.5;10667;0)"</code>
/// The optional fourth field is a per-entry flag that varies between versions and is not
/// needed here. Only Zandronum entries are imported: the launcher protocol differs per engine,
/// so guessing would silently produce dead rows.
/// </remarks>
public static class DoomseekerImporter
{
    /// <summary>The engine whose entries this watcher can query.</summary>
    public const string SupportedEngine = "Zandronum";

    private static readonly Regex EntryPattern = new(@"\(([^()]*)\)", RegexOptions.Compiled);

    /// <summary>The current single-instance config, then the older roaming location.</summary>
    public static IEnumerable<string> CandidatePaths
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            yield return Path.Combine(local, "Doomseeker", "doomseeker.ini");
            yield return Path.Combine(roaming, "Doomseeker", "doomseeker.ini");
            yield return Path.Combine(local, "doomseeker", "doomseeker.ini");
        }
    }

    /// <summary>Returns the first Doomseeker config that exists, or null.</summary>
    public static string? FindConfig() => CandidatePaths.FirstOrDefault(File.Exists);

    /// <summary>Parses the INI text and returns the Zandronum entries it contains.</summary>
    public static DoomseekerImportResult Parse(string iniText, string engine = SupportedEngine)
    {
        var servers = new List<ServerEndpoint>();
        var skipped = new List<string>();
        var warnings = new List<string>();
        var seen = new HashSet<ServerEndpoint>();

        foreach (var line in iniText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("CustomServers", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var value = trimmed[(separator + 1)..].Trim().Trim('"');

            foreach (Match match in EntryPattern.Matches(value))
            {
                var body = match.Groups[1].Value;
                if (body.Length == 0)
                {
                    continue; // @Invalid()
                }

                var fields = body.Split(';');
                if (fields.Length < 3)
                {
                    warnings.Add(body);
                    continue;
                }

                var entryEngine = fields[0].Trim();
                var host = fields[1].Trim();
                var portText = fields[2].Trim();

                if (!string.Equals(entryEngine, engine, StringComparison.OrdinalIgnoreCase))
                {
                    if (!skipped.Contains(entryEngine))
                    {
                        skipped.Add(entryEngine);
                    }

                    continue;
                }

                if (!int.TryParse(portText, out var port) || port is <= 0 or > 65535 || host.Length == 0)
                {
                    warnings.Add(body);
                    continue;
                }

                var endpoint = new ServerEndpoint(host, port);
                if (seen.Add(endpoint))
                {
                    servers.Add(endpoint);
                }
            }
        }

        return new DoomseekerImportResult
        {
            Servers = servers,
            SkippedEngines = skipped,
            Warnings = warnings,
        };
    }
}
