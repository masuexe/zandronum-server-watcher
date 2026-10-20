using System.IO;
using System.Windows;
using ZandronumWatcher.Core.Configuration;
using ZandronumWatcher.Core.Import;
using ZandronumWatcher.Core.Models;
using ZandronumWatcher.Core.Services;

namespace ZandronumWatcher.App.ViewModels;

/// <summary>Drives the add-server dialog: search a cached scan, pin or unpin.</summary>
public sealed class AddServerViewModel : ObservableObject
{
    private readonly string _configPath;
    private readonly WatchConfig _config;
    private readonly ServerDirectoryCache _cache;
    private readonly List<ServerCandidateViewModel> _all = [];
    private IReadOnlyList<ServerCandidateViewModel> _results = [];
    private System.Windows.Threading.DispatcherTimer? _searchDebounce;

    private string _searchText = "";
    private string _manualAddress = "";
    private string _statusLine = "loading…";
    private bool _isScanning;

    public AddServerViewModel(string? configPath = null)
    {
        _configPath = configPath ?? WatchConfig.DefaultPath;
        _config = WatchConfig.Load(_configPath);
        _cache = new ServerDirectoryCache(ServerDirectoryCache.DefaultPathFor(_configPath));

        LoadCachedScan();
    }

    /// <summary>
    /// The filtered list. Replaced wholesale on each change so WPF gets a single reset instead
    /// of one collection-changed notification per row.
    /// </summary>
    public IReadOnlyList<ServerCandidateViewModel> Results
    {
        get => _results;
        private set
        {
            _results = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Raised whenever the pinned set changes, so the overlay can rebuild.</summary>
    public event EventHandler? PinsChanged;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ScheduleFilter();
            }
        }
    }

    public string ManualAddress
    {
        get => _manualAddress;
        set => SetProperty(ref _manualAddress, value);
    }

    public string StatusLine
    {
        get => _statusLine;
        private set => SetProperty(ref _statusLine, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanScan));
            }
        }
    }

    public bool CanScan => !_isScanning;

    public int PinnedCount => _all.Count(c => c.IsPinned);

    /// <summary>Toggles a row's pin state and persists it.</summary>
    public void TogglePin(ServerCandidateViewModel candidate)
    {
        // Flip to the state we are about to apply. Deriving it from the current value with
        // `is false` would always produce true when IsPinned was already true, which made
        // unpinning impossible: the row stayed pinned and the next click tried to pin it again.
        var shouldPin = !candidate.IsPinned;

        if (shouldPin)
        {
            Pin(candidate.Endpoint);
        }
        else
        {
            Unpin(candidate.Endpoint);
        }

        candidate.IsPinned = shouldPin;
        UpdateStatus();
        PinsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pins whatever the user typed into the manual address box.</summary>
    public bool TryPinManualAddress()
    {
        var text = ManualAddress.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        // Accept a bare host too, defaulting to the conventional first server port.
        if (!text.Contains(':') && ServerEndpoint.TryParse(text + ":10666", out _))
        {
            text += ":10666";
        }

        if (!ServerEndpoint.TryParse(text, out var endpoint))
        {
            StatusLine = $"'{text}' is not a valid host:port";
            return false;
        }

        if (Pin(endpoint))
        {
            ManualAddress = "";
            UpdateStatus();
            PinsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        if (IsScanning)
        {
            return;
        }

        IsScanning = true;
        StatusLine = "querying master server…";

        try
        {
            var options = new ServerQueryOptions { MaxConcurrency = 24 };
            var directory = new ServerDirectory(new ZandronumClient(options.Timeout), options);

            var servers = await directory.DiscoverAsync(
                new ServerEndpoint("master.zandronum.com", 15300),
                count =>
                {
                    if (count % 25 == 0)
                    {
                        StatusLine = $"queried {count} server(s)…";
                    }
                },
                cancellationToken).ConfigureAwait(true);

            _cache.Save(servers);
            Populate(servers);
            StatusLine = $"{servers.Count} reachable server(s) — scanned just now";
        }
        catch (OperationCanceledException)
        {
            StatusLine = "scan cancelled";
        }
        catch (Exception ex)
        {
            StatusLine = $"scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void LoadCachedScan()
    {
        var scan = _cache.Load();
        if (scan is null)
        {
            StatusLine = "no cached scan — press Scan to fetch the server list";
            Populate([]);
            return;
        }

        Populate(scan.Servers);
        var age = DateTimeOffset.Now - scan.ScannedAt;
        var ageText = age.TotalMinutes < 1
            ? "just now"
            : $"{(int)age.TotalMinutes} min ago";

        StatusLine = $"{scan.Servers.Count} server(s) from a scan {ageText}";
    }

    private void Populate(IReadOnlyList<ServerInfo> servers)
    {
        var pinned = _config.ParseEndpoints().ToHashSet();

        _all.Clear();
        foreach (var info in servers)
        {
            var endpoint = new ServerEndpoint(info.Host, info.Port);
            _all.Add(new ServerCandidateViewModel(info, pinned.Contains(endpoint)));
        }

        ApplyFilter();
    }

    /// <summary>
    /// Coalesces rapid keystrokes into one filter pass. Filtering itself is cheap, but each
    /// pass resets the list the ListBox is bound to, and doing that per character makes typing
    /// feel heavy even with virtualisation.
    /// </summary>
    private void ScheduleFilter()
    {
        _searchDebounce ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };

        _searchDebounce.Stop();
        _searchDebounce.Tick += OnDebounceTick;
        _searchDebounce.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _searchDebounce!.Stop();
        _searchDebounce.Tick -= OnDebounceTick;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var matches = _all.Where(c => c.Matches(term)).ToList();

        // Pinned first, then populated, then by name: the servers worth pinning are at the top.
        matches.Sort((a, b) =>
        {
            var byPin = b.IsPinned.CompareTo(a.IsPinned);
            if (byPin != 0)
            {
                return byPin;
            }

            var byHumans = b.Info.HumanCount.CompareTo(a.Info.HumanCount);
            return byHumans != 0
                ? byHumans
                : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        });

        Results = matches;

        if (term.Length > 0 && matches.Count == 0)
        {
            StatusLine = $"no server matches '{term}' — you can still pin it by address below";
        }
    }

    private bool Pin(ServerEndpoint endpoint)
    {
        if (!PinList.Pin(_config.Servers, endpoint))
        {
            return false;
        }

        _config.Save(_configPath);
        return true;
    }

    /// <summary>
    /// Pins every row currently visible. Filtering first and pinning in bulk is the common
    /// case ("pin all survival servers"), not clicking 20 individual plus buttons.
    /// </summary>
    public int PinAllVisible()
    {
        var pinned = 0;
        foreach (var candidate in Results.Where(c => !c.IsPinned).ToList())
        {
            if (Pin(candidate.Endpoint))
            {
                candidate.IsPinned = true;
                pinned++;
            }
        }

        if (pinned > 0)
        {
            UpdateStatus();
            PinsChanged?.Invoke(this, EventArgs.Empty);
        }

        return pinned;
    }

    /// <summary>
    /// Copies Doomseeker's custom-server list into the pinned list.
    /// </summary>
    /// <returns>A message describing what happened, suitable for the status line.</returns>
    public string ImportFromDoomseeker()
    {
        var iniPath = DoomseekerImporter.FindConfig();
        if (iniPath is null)
        {
            return "Doomseeker config not found";
        }

        DoomseekerImportResult result;
        try
        {
            result = DoomseekerImporter.Parse(File.ReadAllText(iniPath));
        }
        catch (IOException ex)
        {
            return $"could not read {iniPath}: {ex.Message}";
        }

        if (result.Servers.Count == 0)
        {
            return "no Zandronum servers in Doomseeker's custom list";
        }

        var added = 0;
        var already = 0;
        foreach (var server in result.Servers)
        {
            if (Pin(server))
            {
                added++;
            }
            else
            {
                already++;
            }
        }

        RefreshPinFlags();

        if (added > 0)
        {
            PinsChanged?.Invoke(this, EventArgs.Empty);
        }

        var summary = $"imported {added} from Doomseeker ({already} already pinned)";
        if (result.SkippedEngines.Count > 0)
        {
            summary += $"; skipped {string.Join(", ", result.SkippedEngines)}";
        }

        // Servers pinned from Doomseeker may not exist in the cached scan at all, in which case
        // no row can show a check mark for them. Say so instead of leaving the user to wonder
        // why the count does not match what they see.
        var known = _all.Select(c => c.Endpoint).ToHashSet();
        if (result.Servers.Any(s => !known.Contains(s)))
        {
            summary += "; rescan to list them here (the overlay shows them already)";
        }

        StatusLine = summary;
        return summary;
    }

    /// <summary>Re-reads the config so row check marks match what is actually pinned.</summary>
    private void RefreshPinFlags()
    {
        var fresh = WatchConfig.Load(_configPath);
        _config.Servers = fresh.Servers;

        var pinned = _config.ParseEndpoints().ToHashSet();
        foreach (var candidate in _all)
        {
            candidate.IsPinned = pinned.Contains(candidate.Endpoint);
        }

        ApplyFilter();
    }

    /// <summary>Removes every currently pinned server, whatever the filter shows.</summary>
    public int UnpinAll()
    {
        var unpinned = 0;
        foreach (var candidate in _all.Where(c => c.IsPinned).ToList())
        {
            Unpin(candidate.Endpoint);
            candidate.IsPinned = false;
            unpinned++;
        }

        if (unpinned > 0)
        {
            UpdateStatus();
            PinsChanged?.Invoke(this, EventArgs.Empty);
        }

        return unpinned;
    }

    private void Unpin(ServerEndpoint endpoint)
    {
        if (PinList.Unpin(_config.Servers, endpoint))
        {
            _config.Save(_configPath);
        }
    }

    private void UpdateStatus()
    {
        var pinned = _all.Count(c => c.IsPinned);
        StatusLine = $"{pinned} server(s) pinned";
    }
}