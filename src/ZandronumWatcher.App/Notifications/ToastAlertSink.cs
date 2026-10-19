using Microsoft.Toolkit.Uwp.Notifications;
using ZandronumWatcher.Core.Alerts;

namespace ZandronumWatcher.App.Notifications;

/// <summary>Shows occupancy alerts as Windows toast notifications.</summary>
internal sealed class ToastAlertSink : IAlertSink
{
    private readonly bool _includeDetail;

    public ToastAlertSink(bool includeDetail = true) => _includeDetail = includeDetail;

    public Task NotifyAsync(OccupancyChange change, CancellationToken cancellationToken = default)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(change.Headline);

            if (_includeDetail)
            {
                builder.AddText(change.Detail);
            }

            builder.AddArgument("endpoint", change.Endpoint.ToString());
            builder.Show();
        }
        catch (Exception)
        {
            // Notifications can fail when the shell is unavailable; the overlay still updates.
        }

        return Task.CompletedTask;
    }
}
