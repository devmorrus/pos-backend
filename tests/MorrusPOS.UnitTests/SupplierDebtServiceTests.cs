using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class SupplierDebtServiceTests
{
    private readonly AppDbContext _dbContext;

    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();

    public SupplierDebtServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
    }

    private async Task<SupplierDebt> SeedDebtAsync(decimal amount = 100000m)
    {
        var poId = Guid.NewGuid();

        _dbContext.Suppliers.Add(new Supplier { Id = _supplierId, Name = "PT Maju Jaya", IsActive = true });
        _dbContext.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT-1", Name = "Outlet A", IsActive = true });
        _dbContext.Users.Add(new User { Id = _userId, Name = "Admin", Email = "a@test.com", PasswordHash = "hash", RoleId = _roleId });

        _dbContext.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = poId,
            SupplierId = _supplierId,
            OutletId = _outletId,
            PoNumber = $"PO-TEST-001",
            PaymentType = "tempo",
            Status = "completed",
            DueDate = DateTime.UtcNow.AddDays(30),
            TotalAmount = amount,
            CreatedBy = _userId
        });

        var debt = new SupplierDebt
        {
            Id = Guid.NewGuid(),
            SupplierId = _supplierId,
            PurchaseOrderId = poId,
            DueDate = DateTime.UtcNow.AddDays(30),
            Amount = amount,
            PaidAmount = 0,
            RemainingAmount = amount,
            Status = SupplierDebtStatus.Unpaid
        };

        _dbContext.SupplierDebts.Add(debt);
        await _dbContext.SaveChangesAsync();
        return debt;
    }

    // ===== PARTIAL PAYMENT =====

    [Fact]
    public async Task PayDebtAsync_Should_ReduceRemainingAmount_And_SetStatus_ToPartiallyPaid()
    {
        var debt = await SeedDebtAsync(amount: 100000);
        var service = new SupplierDebtService(_dbContext);

        var request = new CreateSupplierPaymentRequest(
            PurchaseOrderId: debt.PurchaseOrderId,
            Amount: 40000,
            PaymentMethod: "Transfer",
            ReferenceNumber: "TRF-001"
        );

        var result = await service.PayDebtAsync(_userId, request);

        result.Should().NotBeNull();
        result.Amount.Should().Be(40000);
        result.PaymentMethod.Should().Be("Transfer");

        // Verify debt was updated
        var updatedDebt = await _dbContext.SupplierDebts.FindAsync(debt.Id);
        updatedDebt!.PaidAmount.Should().Be(40000);
        updatedDebt.RemainingAmount.Should().Be(60000);
        updatedDebt.Status.Should().Be("partially_paid");
    }

    // ===== FULL PAYMENT =====

    [Fact]
    public async Task PayDebtAsync_Should_SetStatus_ToPaid_When_FullPayment()
    {
        var debt = await SeedDebtAsync(amount: 100000);
        var service = new SupplierDebtService(_dbContext);

        var request = new CreateSupplierPaymentRequest(
            PurchaseOrderId: debt.PurchaseOrderId,
            Amount: 100000, // Full payment
            PaymentMethod: "Cash",
            ReferenceNumber: null
        );

        await service.PayDebtAsync(_userId, request);

        var updatedDebt = await _dbContext.SupplierDebts.FindAsync(debt.Id);
        updatedDebt!.PaidAmount.Should().Be(100000);
        updatedDebt.RemainingAmount.Should().Be(0);
        updatedDebt.Status.Should().Be("paid");
    }

    // ===== OVERPAYMENT GUARD =====

    [Fact]
    public async Task PayDebtAsync_Should_ThrowException_When_PaymentExceedsRemainingAmount()
    {
        var debt = await SeedDebtAsync(amount: 100000);
        var service = new SupplierDebtService(_dbContext);

        var request = new CreateSupplierPaymentRequest(
            PurchaseOrderId: debt.PurchaseOrderId,
            Amount: 150000, // Exceeds remaining!
            PaymentMethod: "Cash",
            ReferenceNumber: null
        );

        var act = () => service.PayDebtAsync(_userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*melebihi sisa utang*");
    }

    // ===== PAYMENT ON ALREADY PAID DEBT =====

    [Fact]
    public async Task PayDebtAsync_Should_ThrowException_When_DebtIsAlreadyPaid()
    {
        var debt = await SeedDebtAsync(amount: 100000);
        var service = new SupplierDebtService(_dbContext);

        // First: pay off the full amount
        await service.PayDebtAsync(_userId, new CreateSupplierPaymentRequest(
            debt.PurchaseOrderId, 100000, "Cash", null));

        // Second: try paying again after it's fully paid
        var act = () => service.PayDebtAsync(_userId, new CreateSupplierPaymentRequest(
            debt.PurchaseOrderId, 10000, "Cash", null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*sudah lunas*");
    }
}
