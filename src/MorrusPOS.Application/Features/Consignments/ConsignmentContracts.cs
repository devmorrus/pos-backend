using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MorrusPOS.Application.Features.Consignments;

// Consignment Receipts
public record ConsignmentItemDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Qty,
    decimal UnitCost,
    decimal UnitPrice,
    decimal SoldQty,
    decimal ReturnedQty
);

public record ConsignmentDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid OutletId,
    string OutletName,
    string ConsignmentNumber,
    DateTime ReceiveDate,
    string Status,
    IReadOnlyList<ConsignmentItemDto> Items
);

public record ConsignmentItemRequest(
    Guid ProductId,
    decimal Qty,
    decimal UnitCost,
    decimal UnitPrice
);

public record CreateConsignmentRequest(
    Guid SupplierId,
    Guid OutletId,
    List<ConsignmentItemRequest> Items
);

public record UpdateConsignmentStatusRequest(
    string Status
);

public interface IConsignmentService
{
    Task<ConsignmentDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ConsignmentDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<ConsignmentDto> CreateAsync(Guid userId, CreateConsignmentRequest request, CancellationToken ct = default);
    Task<ConsignmentDto> UpdateStatusAsync(Guid userId, Guid consignmentId, UpdateConsignmentStatusRequest request, CancellationToken ct = default);
}

// Consignment Sales & Settlements
public record ConsignmentSaleDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid TransactionItemId,
    string TransactionNumber,
    string ProductName,
    decimal Qty,
    decimal UnitCost,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt
);

public record ConsignmentSettlementDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    Guid OutletId,
    string OutletName,
    string SettlementNumber,
    DateTime SettlementDate,
    decimal TotalAmount,
    string Status,
    IReadOnlyList<ConsignmentSaleDto> Sales
);

public record CreateConsignmentSettlementRequest(
    Guid SupplierId,
    Guid OutletId
);

public record UpdateConsignmentSettlementStatusRequest(
    string Status
);

public interface IConsignmentSettlementService
{
    Task<ConsignmentSettlementDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ConsignmentSettlementDto>> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
    Task<IReadOnlyList<ConsignmentSaleDto>> GetUnpaidSalesBySupplierAsync(Guid supplierId, Guid outletId, CancellationToken ct = default);
    Task<ConsignmentSettlementDto> CreateSettlementAsync(Guid userId, CreateConsignmentSettlementRequest request, CancellationToken ct = default);
    Task<ConsignmentSettlementDto> UpdateStatusAsync(Guid userId, Guid settlementId, UpdateConsignmentSettlementStatusRequest request, CancellationToken ct = default);
}
