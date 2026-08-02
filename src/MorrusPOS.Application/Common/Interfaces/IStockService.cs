namespace MorrusPOS.Application.Common.Interfaces;

public interface IStockService
{
    Task AddMovementAsync(
        Guid productId,
        Guid outletId,
        decimal qtyChange,
        string movementType,
        string referenceType,
        Guid referenceId,
        string? note = null,
        CancellationToken ct = default);
}
