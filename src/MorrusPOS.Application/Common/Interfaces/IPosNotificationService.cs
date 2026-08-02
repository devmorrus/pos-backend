namespace MorrusPOS.Application.Common.Interfaces;

public record StockUpdateItem(Guid ProductId, decimal Qty);

public interface IPosNotificationService
{
    Task SendStockUpdateAsync(Guid outletId, List<StockUpdateItem> updates, CancellationToken ct = default);
}
