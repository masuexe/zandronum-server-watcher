using System.Windows;
using System.Windows.Media;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.App.ViewModels;

/// <summary>One card in the overlay: a watched server and its latest status.</summary>
public sealed class ServerRowViewModel : ObservableObject
{
    private static readonly Brush OnlineBrush = Freeze(Color.FromRgb(0x6F, 0xD0, 0x8C));
    private static readonly Brush OfflineBrush = Freeze(Color.FromRgb(0x9A, 0x5A, 0x5A));
    private static readonly Brush PendingBrush = Freeze(Color.FromRgb(0x5F, 0x66, 0x6E));

    private ServerStatus _status;
    private bool _isExpanded;

    public ServerRowViewModel(ServerEndpoint endpoint)
    {
        Endpoint = endpoint;
        _status = ServerStatus.Offline(endpoint, "waiting for first refresh");
    }

    public ServerEndpoint Endpoint { get; }

    public string Address => Endpoint.ToString();

    public ServerStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(SlotText));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(Players));
                OnPropertyChanged(nameof(PlayerSummary));
                OnPropertyChanged(nameof(Tooltip));
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(PlayerVisibility));
            }
        }
    }

    public Visibility PlayerVisibility => IsExpanded && IsOnline ? Visibility.Visible : Visibility.Collapsed;

    public bool IsOnline => _status.IsOnline;

    public string Title
    {
        get
        {
            if (_status.Info is { } info)
            {
                return string.IsNullOrWhiteSpace(info.DisplayName) ? Address : info.DisplayName;
            }

            return Address;
        }
    }

    public string Subtitle
    {
        get
        {
            if (_status.Info is { } info)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(info.GameModeName))
                {
                    parts.Add(info.GameModeName);
                }

                if (!string.IsNullOrEmpty(info.MapName))
                {
                    parts.Add(info.MapName);
                }

                return string.Join("  ·  ", parts);
            }

            return _status.TimedOut
                ? "no reply — retrying"
                : _status.Error ?? "offline";
        }
    }

    public string SlotText => _status.Info is { } info ? info.SlotText : "—";

    public Brush StatusBrush => _status.Info is not null
        ? OnlineBrush
        : _status.TimedOut || _status.Error == "waiting for first refresh"
            ? PendingBrush
            : OfflineBrush;

    public IReadOnlyList<PlayerInfo> Players => _status.Info?.Players ?? [];

    public string PlayerSummary
    {
        get
        {
            if (_status.Info is not { } info)
            {
                return "";
            }

            var summary = $"{info.HumanCount} player(s)";
            if (info.BotCount > 0)
            {
                summary += $", {info.BotCount} bot(s)";
            }

            return summary;
        }
    }

    public string Tooltip
    {
        get
        {
            if (_status.Info is not { } info)
            {
                return $"{Address}\n{_status.Error}";
            }

            var lines = new List<string>
            {
                Address,
                info.DisplayName,
                $"{info.GameModeName}  ·  {info.MapName}  ·  {info.SlotText}",
                $"ping {info.Latency.TotalMilliseconds:F0} ms  ·  {info.Version}",
            };

            if (!string.IsNullOrEmpty(info.Iwad))
            {
                lines.Add($"iwad: {info.Iwad}");
            }

            if (info.Pwads.Count > 0)
            {
                lines.Add($"pwads: {string.Join(", ", info.Pwads)}");
            }

            return string.Join("\n", lines);
        }
    }

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}