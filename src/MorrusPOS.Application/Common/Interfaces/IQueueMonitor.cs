using MorrusPOS.Application.Common.Monitoring;

namespace MorrusPOS.Application.Common.Interfaces;

public interface IQueueMonitor
{
    string QueueName { get; }
    Task<QueueSnapshot> GetSnapshotAsync(CancellationToken ct = default);
}
