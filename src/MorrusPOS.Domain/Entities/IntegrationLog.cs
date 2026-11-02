using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class IntegrationLog : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string ServiceName { get; set; } = default!; // e.g. "GoFood Webhook", "GrabFood API"
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
