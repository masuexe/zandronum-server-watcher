namespace ZandronumWatcher.Core.Alerts;

/// <summary>Delivers occupancy alerts to the user.</summary>
public interface IAlertSink
{
    /// <summary>Presents one alert. Implementations must not throw.</summary>
    Task NotifyAsync(OccupancyChange change, CancellationToken cancellationToken = default);
}

/// <summary>Writes alerts to the console; useful headless and in tests.</summary>
public sealed class ConsoleAlertSink : IAlertSink
{
    public Task NotifyAsync(OccupancyChange change, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] {change.Headline}");
        Console.WriteLine($"           {change.Detail}");
        return Task.CompletedTask;
    }
}
