using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Reports;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class AccountingIntegrationServiceTests
{
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly AppDbContext _dbContext;
    private readonly TestCurrentUserService _currentUser;

    public AccountingIntegrationServiceTests()
    {
        _currentUser = new TestCurrentUserService
        {
            BusinessId = _businessId,
            OutletId = null,
            Role = "Admin",
            UserId = Guid.NewGuid(),
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options, _currentUser);

        var roleId = Guid.NewGuid();
        _dbContext.Businesses.Add(new Business
        {
            Id = _businessId,
            Name = "Biz A",
            Category = "Retail",
        });
        _dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Name = "Admin",
        });
        _dbContext.Outlets.Add(new Outlet
        {
            Id = _outletId,
            BusinessId = _businessId,
            Code = "OUT-A",
            Name = "Outlet A",
            IsActive = true,
        });
        _dbContext.Users.Add(new User
        {
            Id = _currentUser.UserId!.Value,
            BusinessId = _businessId,
            OutletId = _outletId,
            RoleId = roleId,
            Name = "Tester",
            Email = "tester@example.com",
            PasswordHash = "hash",
            IsActive = true,
        });
        _dbContext.Categories.Add(new Category
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Name = "General",
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task EnsureTransactionPostedAsync_Should_CreateBalancedEntries_AndFeedReports()
    {
        var cash = AddAccount("1010", "Kas Utama", ChartOfAccountType.Asset, true);
        var inventory = AddAccount("1200", "Persediaan Barang", ChartOfAccountType.Asset, false);
        var revenue = AddAccount("4100", "Pendapatan Penjualan", ChartOfAccountType.Revenue, false);
        var cogs = AddAccount("5100", "Harga Pokok Penjualan", ChartOfAccountType.Cogs, false);
        await _dbContext.SaveChangesAsync();

        var categoryId = _dbContext.Categories.Select(c => c.Id).First();
        var productId = Guid.NewGuid();
        _dbContext.Products.Add(new Product
        {
            Id = productId,
            BusinessId = _businessId,
            CategoryId = categoryId,
            Sku = "SKU-001",
            Name = "Produk A",
            BasePrice = 100,
            CostPrice = 40,
            Unit = "pcs",
            IsActive = true,
        });

        var transactionId = Guid.NewGuid();
        _dbContext.Transactions.Add(new Transaction
        {
            Id = transactionId,
            OutletId = _outletId,
            UserId = _currentUser.UserId!.Value,
            TransactionNumber = "TRX-001",
            Channel = TransactionChannel.Pos,
            Status = TransactionStatus.Completed,
            CustomerType = TransactionCustomerType.Guest,
            Subtotal = 200,
            DiscountTotal = 0,
            ManualDiscountTotal = 0,
            PromoDiscountTotal = 0,
            VoucherDiscountTotal = 0,
            ServiceChargeTotal = 0,
            TaxTotal = 0,
            GrandTotal = 200,
            CreatedAt = new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc),
            Items =
            [
                new TransactionItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Qty = 2,
                    UnitPrice = 100,
                    UnitCost = 40,
                    DiscountAmount = 0,
                    LineTotal = 200,
                },
            ],
            Payments =
            [
                new Payment
                {
                    Id = Guid.NewGuid(),
                    Method = PaymentMethod.Cash,
                    Amount = 200,
                },
            ],
        });
        await _dbContext.SaveChangesAsync();

        var service = new AccountingIntegrationService(_dbContext, _currentUser);
        var firstRun = await service.EnsureTransactionPostedAsync(transactionId);
        var secondRun = await service.EnsureTransactionPostedAsync(transactionId);

        firstRun.Should().BeTrue();
        secondRun.Should().BeFalse();

        var entries = await _dbContext.AccountTransactions
            .Where(entry => entry.ReferenceType == "transaction_sale" && entry.ReferenceId == transactionId)
            .OrderBy(entry => entry.ChartOfAccount.AccountCode)
            .ToListAsync();

        entries.Should().HaveCount(4);
        entries.Sum(entry => entry.DebitAmount).Should().Be(entries.Sum(entry => entry.CreditAmount));
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == cash.Id && entry.DebitAmount == 200);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == revenue.Id && entry.CreditAmount == 200);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == cogs.Id && entry.DebitAmount == 80);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == inventory.Id && entry.CreditAmount == 80);

        var reportService = new ReportService(_dbContext, _currentUser);
        var cashFlowReport = await reportService.GetCashFlowReportAsync(new AccountingCashFlowReportFilters(
            new DateTime(2026, 8, 1),
            new DateTime(2026, 8, 31),
            _outletId,
            cash.Id,
            null));
        var profitLossReport = await reportService.GetAccountingProfitLossReportAsync(new AccountingProfitLossReportFilters(
            new DateTime(2026, 8, 1),
            new DateTime(2026, 8, 31),
            _outletId,
            null));

        cashFlowReport.Summary.CashIn.Should().Be(200);
        profitLossReport.Summary.RevenueTotal.Should().Be(200);
        profitLossReport.Summary.CogsTotal.Should().Be(80);
    }

    [Fact]
    public async Task EnsurePurchaseOrderPostedAsync_Should_PostTempoPurchaseToInventoryAndPayable()
    {
        var inventory = AddAccount("1200", "Persediaan Barang", ChartOfAccountType.Asset, false);
        var payable = AddAccount("2100", "Utang Supplier", ChartOfAccountType.Liability, false);
        await _dbContext.SaveChangesAsync();

        var purchaseOrderId = Guid.NewGuid();
        _dbContext.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = purchaseOrderId,
            SupplierId = AddSupplier().Id,
            OutletId = _outletId,
            PoNumber = "PO-001",
            PoDate = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc),
            PaymentType = PurchaseOrderPaymentType.Tempo,
            Status = PurchaseOrderStatus.Completed,
            CreatedBy = _currentUser.UserId!.Value,
            TotalAmount = 300,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    Qty = 3,
                    UnitCost = 100,
                    TotalCost = 300,
                },
            ],
        });
        await _dbContext.SaveChangesAsync();

        var service = new AccountingIntegrationService(_dbContext, _currentUser);
        var posted = await service.EnsurePurchaseOrderPostedAsync(purchaseOrderId);

        posted.Should().BeTrue();
        var entries = await GetEntriesAsync("purchase_order", purchaseOrderId);
        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == inventory.Id && entry.DebitAmount == 300);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == payable.Id && entry.CreditAmount == 300);
    }

    [Fact]
    public async Task EnsureSupplierPaymentPostedAsync_Should_DebitPayableAndCreditCash()
    {
        var cash = AddAccount("1010", "Kas Utama", ChartOfAccountType.Asset, true);
        var payable = AddAccount("2100", "Utang Supplier", ChartOfAccountType.Liability, false);
        await _dbContext.SaveChangesAsync();

        var supplier = AddSupplier();
        var purchaseOrderId = Guid.NewGuid();
        _dbContext.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = purchaseOrderId,
            SupplierId = supplier.Id,
            OutletId = _outletId,
            PoNumber = "PO-002",
            PoDate = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc),
            PaymentType = PurchaseOrderPaymentType.Tempo,
            Status = PurchaseOrderStatus.Completed,
            CreatedBy = _currentUser.UserId!.Value,
            TotalAmount = 300,
        });

        var paymentId = Guid.NewGuid();
        _dbContext.SupplierPayments.Add(new SupplierPayment
        {
            Id = paymentId,
            SupplierId = supplier.Id,
            PurchaseOrderId = purchaseOrderId,
            PaymentDate = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc),
            Amount = 150,
            PaymentMethod = PaymentMethod.Cash,
            Status = SupplierPaymentStatus.Paid,
            CreatedBy = _currentUser.UserId!.Value,
        });
        await _dbContext.SaveChangesAsync();

        var service = new AccountingIntegrationService(_dbContext, _currentUser);
        var posted = await service.EnsureSupplierPaymentPostedAsync(paymentId);

        posted.Should().BeTrue();
        var entries = await GetEntriesAsync("supplier_payment", paymentId);
        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == payable.Id && entry.DebitAmount == 150);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == cash.Id && entry.CreditAmount == 150);
    }

    [Fact]
    public async Task EnsureSupplierReturnPostedAsync_Should_DebitPayableAndCreditInventory()
    {
        var inventory = AddAccount("1200", "Persediaan Barang", ChartOfAccountType.Asset, false);
        var payable = AddAccount("2100", "Utang Supplier", ChartOfAccountType.Liability, false);
        await _dbContext.SaveChangesAsync();

        var supplier = AddSupplier();
        var purchaseOrderId = Guid.NewGuid();
        _dbContext.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = purchaseOrderId,
            SupplierId = supplier.Id,
            OutletId = _outletId,
            PoNumber = "PO-003",
            PoDate = new DateTime(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc),
            PaymentType = PurchaseOrderPaymentType.Tempo,
            Status = PurchaseOrderStatus.Completed,
            CreatedBy = _currentUser.UserId!.Value,
            TotalAmount = 200,
        });

        var supplierReturnId = Guid.NewGuid();
        _dbContext.SupplierReturns.Add(new SupplierReturn
        {
            Id = supplierReturnId,
            SupplierId = supplier.Id,
            PurchaseOrderId = purchaseOrderId,
            ReturnNumber = "SR-001",
            ReturnDate = new DateTime(2026, 8, 13, 14, 0, 0, DateTimeKind.Utc),
            Status = SupplierReturnStatus.Sent,
            TotalAmount = 50,
            CreatedBy = _currentUser.UserId!.Value,
        });
        await _dbContext.SaveChangesAsync();

        var service = new AccountingIntegrationService(_dbContext, _currentUser);
        var posted = await service.EnsureSupplierReturnPostedAsync(supplierReturnId);

        posted.Should().BeTrue();
        var entries = await GetEntriesAsync("supplier_return", supplierReturnId);
        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == payable.Id && entry.DebitAmount == 50);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == inventory.Id && entry.CreditAmount == 50);
    }

    [Fact]
    public async Task EnsureChannelSettlementPostedAsync_Should_PostNetFeeAndClearing()
    {
        var bank = AddAccount("1020", "Bank Utama", ChartOfAccountType.Asset, true);
        var clearing = AddAccount("1300", "Piutang Channel GrabFood", ChartOfAccountType.Asset, false);
        var feeExpense = AddAccount("6100", "Biaya Fee Channel", ChartOfAccountType.Expense, false);
        await _dbContext.SaveChangesAsync();

        var channelAccountId = Guid.NewGuid();
        _dbContext.ChannelAccounts.Add(new ChannelAccount
        {
            Id = channelAccountId,
            OutletId = _outletId,
            Name = "GrabFood Main",
            ChannelName = TransactionChannel.GrabFood,
            MerchantId = "grab-main",
            DefaultCommissionRate = 20,
            IsActive = true,
        });

        var settlementId = Guid.NewGuid();
        _dbContext.ChannelSettlements.Add(new ChannelSettlement
        {
            Id = settlementId,
            ChannelAccountId = channelAccountId,
            SettlementNumber = "CHSET-001",
            SettlementDate = new DateTime(2026, 8, 13, 15, 0, 0, DateTimeKind.Utc),
            PeriodStartDate = new DateTime(2026, 8, 1),
            PeriodEndDate = new DateTime(2026, 8, 13),
            GrossAmount = 200,
            CommissionAmount = 20,
            NetAmount = 180,
            Status = ChannelSettlementStatus.Settled,
            CreatedBy = _currentUser.UserId!.Value,
        });
        await _dbContext.SaveChangesAsync();

        var service = new AccountingIntegrationService(_dbContext, _currentUser);
        var posted = await service.EnsureChannelSettlementPostedAsync(settlementId);

        posted.Should().BeTrue();
        var entries = await GetEntriesAsync("channel_settlement", settlementId);
        entries.Should().HaveCount(3);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == bank.Id && entry.DebitAmount == 180);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == feeExpense.Id && entry.DebitAmount == 20);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == clearing.Id && entry.CreditAmount == 200);
    }

    [Fact]
    public async Task EnsureConsignmentSettlementPostedAsync_Should_DebitPayableAndCreditCash()
    {
        var bank = AddAccount("1020", "Bank Utama", ChartOfAccountType.Asset, true);
        var consignmentPayable = AddAccount("2200", "Utang Konsinyasi", ChartOfAccountType.Liability, false);
        await _dbContext.SaveChangesAsync();

        var supplier = AddSupplier();
        var settlementId = Guid.NewGuid();
        _dbContext.ConsignmentSettlements.Add(new ConsignmentSettlement
        {
            Id = settlementId,
            SupplierId = supplier.Id,
            OutletId = _outletId,
            SettlementNumber = "SET-001",
            SettlementDate = new DateTime(2026, 8, 13, 16, 0, 0, DateTimeKind.Utc),
            TotalAmount = 120,
            Status = ConsignmentSettlementStatus.Settled,
            CreatedBy = _currentUser.UserId!.Value,
        });
        await _dbContext.SaveChangesAsync();

        var service = new AccountingIntegrationService(_dbContext, _currentUser);
        var posted = await service.EnsureConsignmentSettlementPostedAsync(settlementId);

        posted.Should().BeTrue();
        var entries = await GetEntriesAsync("consignment_settlement", settlementId);
        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == consignmentPayable.Id && entry.DebitAmount == 120);
        entries.Should().ContainSingle(entry => entry.ChartOfAccountId == bank.Id && entry.CreditAmount == 120);
    }

    private ChartOfAccount AddAccount(string code, string name, string type, bool isCashBank)
    {
        var account = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            OutletId = _outletId,
            AccountCode = code,
            AccountName = name,
            AccountType = type,
            IsCashBank = isCashBank,
            IsActive = true,
        };

        _dbContext.ChartOfAccounts.Add(account);
        return account;
    }

    private Supplier AddSupplier()
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Name = $"Supplier-{Guid.NewGuid():N}".Substring(0, 16),
            IsActive = true,
        };

        _dbContext.Suppliers.Add(supplier);
        return supplier;
    }

    private async Task<List<AccountTransaction>> GetEntriesAsync(string referenceType, Guid referenceId)
    {
        return await _dbContext.AccountTransactions
            .Where(entry => entry.ReferenceType == referenceType && entry.ReferenceId == referenceId)
            .ToListAsync();
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public Guid? OutletId { get; set; }
        public Guid? BusinessId { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated => true;
    }
}
