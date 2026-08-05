using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MorrusPOS.Application.Features.Consignments;

public record ConsignmentReturnItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty
);

public record ConsignmentReturnDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid OutletId,
    string OutletName,
    string ReturnNumber,
    DateTime ReturnDate,
    string Status,
    IReadOnlyList<ConsignmentReturnItemDto> Items
);

public record ConsignmentReturnItemRequest(
    Guid ProductId,
    decimal Qty
);

public record CreateConsignmentReturnRequest(
    Guid SupplierId,
    Guid OutletId,
    List<ConsignmentReturnItemRequest> Items
);

public record UpdateConsignmentReturnStatusRequest(
    string Status
);

public interface IConsignmentReturnService
{
    Task<ConsignmentReturnDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ConsignmentReturnDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<ConsignmentReturnDto> CreateAsync(Guid userId, CreateConsignmentReturnRequest request, CancellationToken ct = default);
    Task<ConsignmentReturnDto> UpdateStatusAsync(Guid userId, Guid returnId, UpdateConsignmentReturnStatusRequest request, CancellationToken ct = default);
}
