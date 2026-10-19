using System.Windows;
using System.Windows.Media;
using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.App.ViewModels;

/// <summary>One row in the add-server list: a discovered server the user may pin.</summary>
public sealed class ServerCandidateViewModel : ObservableObject
{
    private static readonly Brush OnlineBrush = Freeze(Color.FromRgb(0x6F, 0xD0, 0x8C));
    private static readonly Brush IdleBrush = Freeze(Color.FromRgb(0x5F, 0x66, 0x6E));
    private static readonly Brush PinnedBrush = Freeze(Color.FromRgb(0x5A, 0x8F, 0xC0));

    private bool _isPinned;

    public ServerCandidateViewModel(ServerInfo info, bool isPinned)
    {
        Info = info;
        _isPinned = isPinned;
    }

    public ServerInfo Info { get; }

    public ServerEndpoint Endpoint => new(Info.Host, Info.Port);

    public string Address => Info.Address;

    public string Title => string.IsNullOrWhiteSpace(Info.DisplayName) ? Info.Address : Info.DisplayName;

    public string Detail
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Info.GameModeName))
            {
                parts.Add(Info.GameModeName);
            }

            if (!string.IsNullOrWhiteSpace(Info.MapName))
            {
                parts.Add(Info.MapName);
            }

            return string.Join("  ·  ", parts);
        }
    }

    public string SlotText => Info.SlotText;

    public Brush SlotBrush => Info.HumanCount > 0 ? OnlineBrush : IdleBrush;

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                OnPropertyChanged(nameof(PinGlyph));
                OnPropertyChanged(nameof(PinBrush));
                OnPropertyChanged(nameof(PinTooltip));
            }
        }
    }

    public string PinGlyph => _isPinned ? "✓" : "＋";

    public Brush PinBrush => _isPinned ? PinnedBrush : IdleBrush;

    public string PinTooltip => _isPinned ? "Pinned — click to unpin" : "Click to pin this server";

    /// <summary>True when the row matches a search term over name, map, mode, iwad.</summary>
    public bool Matches(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        return Contains(Info.DisplayName, term)
               || Contains(Info.GameName, term)
               || Contains(Info.MapName, term)
               || Contains(Info.GameModeName, term)
               || Contains(Info.Iwad, term)
               || Contains(Info.Address, term);
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
