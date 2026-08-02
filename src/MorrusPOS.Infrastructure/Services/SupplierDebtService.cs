using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class SupplierDebtService : ISupplierDebtService
{
    private readonly AppDbContext _dbContext;

    public SupplierDebtService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SupplierDebtDto>> GetDebtsAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.SupplierDebts
            .Include(d => d.Supplier)
            .Include(d => d.PurchaseOrder)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);

        var debts = await query.OrderByDescending(d => d.DueDate).ToListAsync(ct);
        return debts.Select(MapDebtToDto).ToList();
    }

    public async Task<SupplierDebtDto> GetDebtByPoIdAsync(Guid purchaseOrderId, CancellationToken ct = default)
    {
        var debt = await _dbContext.SupplierDebts
            .Include(d => d.Supplier)
            .Include(d => d.PurchaseOrder)
            .FirstOrDefaultAsync(d => d.PurchaseOrderId == purchaseOrderId, ct);

        if (debt == null)
            throw new InvalidOperationException("Tidak ada utang untuk Purchase Order tersebut.");

        return MapDebtToDto(debt);
    }

    public async Task<IReadOnlyList<SupplierPaymentDto>> GetPaymentsAsync(CancellationToken ct = default)
    {
        var payments = await _dbContext.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseOrder)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(ct);

        return payments.Select(MapPaymentToDto).ToList();
    }

    public async Task<SupplierPaymentDto> PayDebtAsync(Guid userId, CreateSupplierPaymentRequest request, CancellationToken ct = default)
    {
        // 1. Find the debt for the given PO
        var debt = await _dbContext.SupplierDebts
            .Include(d => d.Supplier)
            .Include(d => d.PurchaseOrder)
            .FirstOrDefaultAsync(d => d.PurchaseOrderId == request.PurchaseOrderId, ct);

        if (debt == null)
            throw new InvalidOperationException("Tidak ada utang untuk Purchase Order tersebut.");

        if (debt.Status == SupplierDebtStatus.Paid)
            throw new InvalidOperationException("Utang ini sudah lunas.");

        // 2. Validate payment amount
        if (request.Amount <= 0)
            throw new InvalidOperationException("Jumlah pembayaran harus lebih dari 0.");

        if (request.Amount > debt.RemainingAmount)
            throw new InvalidOperationException(
                $"Jumlah pembayaran (Rp {request.Amount:N0}) melebihi sisa utang (Rp {debt.RemainingAmount:N0}).");

        // 3. Update debt balance
        debt.PaidAmount += request.Amount;
        debt.RemainingAmount -= request.Amount;
        debt.UpdatedAt = DateTime.UtcNow;

        // 4. Update debt status
        debt.Status = debt.RemainingAmount == 0
            ? SupplierDebtStatus.Paid
            : SupplierDebtStatus.PartiallyPaid;

        // 5. Record payment entry
        var payment = new SupplierPayment
        {
            Id = Guid.NewGuid(),
            SupplierId = debt.SupplierId,
            PurchaseOrderId = debt.PurchaseOrderId,
            PaymentDate = DateTime.UtcNow,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber,
            Status = SupplierPaymentStatus.Paid,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.SupplierPayments.Add(payment);
        await _dbContext.SaveChangesAsync(ct);

        // Reload with navigation properties for response
        await _dbContext.Entry(payment).Reference(p => p.Supplier).LoadAsync(ct);
        await _dbContext.Entry(payment).Reference(p => p.PurchaseOrder).LoadAsync(ct);

        return MapPaymentToDto(payment);
    }

    private static SupplierDebtDto MapDebtToDto(SupplierDebt d) => new(
        d.Id,
        d.SupplierId,
        d.Supplier?.Name ?? string.Empty,
        d.PurchaseOrderId,
        d.PurchaseOrder?.PoNumber ?? string.Empty,
        d.DueDate,
        d.Amount,
        d.PaidAmount,
        d.RemainingAmount,
        d.Status
    );

    private static SupplierPaymentDto MapPaymentToDto(SupplierPayment p) => new(
        p.Id,
        p.SupplierId,
        p.Supplier?.Name ?? string.Empty,
        p.PurchaseOrderId,
        p.PurchaseOrder?.PoNumber ?? string.Empty,
        p.PaymentDate,
        p.Amount,
        p.PaymentMethod,
        p.ReferenceNumber,
        p.Status
    );
}
