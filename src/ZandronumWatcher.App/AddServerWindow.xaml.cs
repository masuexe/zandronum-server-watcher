using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZandronumWatcher.App.ViewModels;

namespace ZandronumWatcher.App;

/// <summary>The add-server dialog: search a scan, pin by click, or paste an address.</summary>
public partial class AddServerWindow : Window
{
    private readonly AddServerViewModel _viewModel;

    public AddServerWindow(string? configPath = null)
    {
        InitializeComponent();

        _viewModel = new AddServerViewModel(configPath);
        DataContext = _viewModel;

        Loaded += (_, _) => SearchBox.Focus();
    }

    /// <summary>Raised when the pinned set changed, so the overlay can refresh.</summary>
    public event EventHandler? PinsChanged;

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.ScanAsync();
    }

    private void OnTogglePinClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ServerCandidateViewModel candidate })
        {
            _viewModel.TogglePin(candidate);
            PinsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPinManualClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TryPinManualAddress())
        {
            PinsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDoneClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Enter in the manual box pins without reaching for the mouse.
        if (e.Key == Key.Enter && ManualBox.IsKeyboardFocusWithin)
        {
            OnPinManualClick(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }

        base.OnPreviewKeyDown(e);
    }
}
