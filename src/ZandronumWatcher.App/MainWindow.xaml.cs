using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ZandronumWatcher.Core.Configuration;
using ZandronumWatcher.App.ViewModels;

namespace ZandronumWatcher.App;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly OverlayViewModel _viewModel;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new OverlayViewModel(WatchConfig.Load());
        DataContext = _viewModel;

        RestorePosition();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Keep the overlay out of Alt+Tab and stop it stealing focus from the game.
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ServerRowViewModel row })
        {
            _viewModel.ToggleExpanded(row);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshNowAsync();
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPinned = !_viewModel.IsPinned;
        Topmost = _viewModel.IsPinned;
        PinButton.Foreground = _viewModel.IsPinned
            ? (System.Windows.Media.Brush)FindResource("AccentOnline")
            : (System.Windows.Media.Brush)FindResource("TextMuted");
    }

    private void OnAddServerClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AddServerWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        // The overlay is WS_EX_NOACTIVATE, so it cannot hand focus to a dialog on its own.
        dialog.ShowInTaskbar = true;
        dialog.PinsChanged += (_, _) => _viewModel.ReloadFromDisk();

        dialog.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void RestorePosition()
    {
        var settings = Properties.Settings.Default;
        if (settings.WindowLeft is double left && settings.WindowTop is double top
            && IsOnScreen(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
            return;
        }

        var workArea = SystemParameters.WorkArea;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = workArea.Right - Width - 24;
        Top = workArea.Top + 24;
    }

    private static bool IsOnScreen(double left, double top)
    {
        var workArea = SystemParameters.WorkArea;
        return left >= workArea.Left - 50
               && top >= workArea.Top - 50
               && left < workArea.Right - 50
               && top < workArea.Bottom - 50;
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = Properties.Settings.Default;
        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        settings.Save();

        await _viewModel.DisposeAsync();
    }
}