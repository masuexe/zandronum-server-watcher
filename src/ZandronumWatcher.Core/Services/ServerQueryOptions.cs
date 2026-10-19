namespace ZandronumWatcher.Core.Services;

/// <summary>Tuning knobs for the polling loop.</summary>
public sealed class ServerQueryOptions
{
    /// <summary>
    /// Delay between two queries of the same server. Zandronum ignores repeat queries for
    /// roughly 10-12 seconds (sv_queryignoretime), so this must stay above that window.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Per-query UDP timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>How many servers are queried concurrently.</summary>
    public int MaxConcurrency { get; set; } = 8;
}
