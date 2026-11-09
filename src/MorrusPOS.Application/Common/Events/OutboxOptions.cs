namespace MorrusPOS.Application.Common.Events;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public bool Enabled { get; set; } = true;
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int BatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = 8;
    public int BaseRetrySeconds { get; set; } = 5;
    public int MaxRetrySeconds { get; set; } = 900;
    public int HandlerTimeoutSeconds { get; set; } = 30;
    public int LeaseSeconds { get; set; } = 120;
    public int ProcessedRetentionDays { get; set; } = 14;
    public int MaxPayloadBytes { get; set; } = 262_144;
}

public static class OutboxRetrySchedule
{
    public static TimeSpan Calculate(int attempt, OutboxOptions options, double jitterFraction)
    {
        var exponent = Math.Clamp(attempt - 1, 0, 30);
        var seconds = Math.Min(options.MaxRetrySeconds, options.BaseRetrySeconds * Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(seconds * (1 + Math.Clamp(jitterFraction, 0, 0.2)));
    }
}
