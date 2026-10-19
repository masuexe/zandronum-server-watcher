using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ZandronumWatcher.Core.Configuration;
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

    private string _searchText = "";
    private string _manualAddress = "";
    private string _statusLine = "loading…";
    private bool _isScanning;

    public AddServerViewModel(string? configPath = null)
    {
        _configPath = configPath ?? WatchConfig.DefaultPath;
        _config = WatchConfig.Load(_configPath);
        _cache = new ServerDirectoryCache(ServerDirectoryCache.DefaultPathFor(_configPath));
        Results = [];

        LoadCachedScan();
    }

    public ObservableCollection<ServerCandidateViewModel> Results { get; }

    /// <summary>Raised whenever the pinned set changes, so the overlay can rebuild.</summary>
    public event EventHandler? PinsChanged;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
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
        if (candidate.IsPinned)
        {
            Unpin(candidate.Endpoint);
        }
        else
        {
            Pin(candidate.Endpoint);
        }

        candidate.IsPinned = candidate.IsPinned is false;
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

        Results.Clear();
        foreach (var match in matches)
        {
            Results.Add(match);
        }

        if (term.Length > 0 && matches.Count == 0)
        {
            StatusLine = $"no server matches '{term}' — you can still pin it by address below";
        }
    }

    private bool Pin(ServerEndpoint endpoint)
    {
        if (_config.Servers.Any(s => ServerEndpoint.TryParse(s.Address, out var e) && e == endpoint))
        {
            return false;
        }

        _config.Servers.Add(new ServerEntry { Address = endpoint.ToString() });
        _config.Save(_configPath);
        return true;
    }

    private void Unpin(ServerEndpoint endpoint)
    {
        var existing = _config.Servers.FirstOrDefault(
            s => ServerEndpoint.TryParse(s.Address, out var e) && e == endpoint);

        if (existing is not null)
        {
            _config.Servers.Remove(existing);
            _config.Save(_configPath);
        }
    }

    private void UpdateStatus()
    {
        var pinned = _all.Count(c => c.IsPinned);
        StatusLine = $"{pinned} server(s) pinned";
    }
}
