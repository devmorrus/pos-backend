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
        CancellationToken ct = default,
        Guid? productVariantId = null,
        Guid? productBatchId = null,
        Guid? productSerialId = null,
        Guid? documentItemId = null);
}
