using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Reports;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService? _currentUserService;

    public ReportService(AppDbContext dbContext)
        : this(dbContext, null)
    {
    }

    public ReportService(AppDbContext dbContext, ICurrentUserService? currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<AccountingCashFlowReportDto> GetCashFlowReportAsync(
        AccountingCashFlowReportFilters filters,
        CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var (startUtc, endUtc) = NormalizePeriod(filters.DateFrom, filters.DateTo);
        var outletId = await ResolveAccessibleOutletIdAsync(filters.OutletId, ct);

        if (filters.ChartOfAccountId.HasValue)
        {
            var accountExists = await _dbContext.ChartOfAccounts
                .AsNoTracking()
                .AnyAsync(account => account.Id == filters.ChartOfAccountId.Value, ct);
            if (!accountExists)
            {
                throw new InvalidOperationException("Akun filter tidak ditemukan.");
            }
        }

        var baseQuery = _dbContext.AccountTransactions
            .Include(entry => entry.ChartOfAccount)
            .Include(entry => entry.Outlet)
            .AsNoTracking()
            .Where(entry => entry.BusinessId == businessId)
            .Where(entry => entry.TrxEntity == AccountingTransactionEntity.Business)
            .Where(entry => entry.ChartOfAccount.AccountType == ChartOfAccountType.Asset)
            .Where(entry => entry.ChartOfAccount.IsCashBank);

        if (outletId.HasValue)
        {
            baseQuery = baseQuery.Where(entry => entry.OutletId == outletId.Value);
        }

        if (filters.ChartOfAccountId.HasValue)
        {
            baseQuery = baseQuery.Where(entry => entry.ChartOfAccountId == filters.ChartOfAccountId.Value);
        }

        var openingBalance = await baseQuery
            .Where(entry => entry.TrxDate < startUtc)
            .SumAsync(entry => entry.DebitAmount - entry.CreditAmount, ct);

        var periodQuery = baseQuery
            .Where(entry => entry.TrxDate >= startUtc && entry.TrxDate <= endUtc);

        if (!string.IsNullOrWhiteSpace(filters.Keyword))
        {
            var keyword = filters.Keyword.Trim().ToLowerInvariant();
            periodQuery = periodQuery.Where(entry =>
                entry.TrxNumber.ToLower().Contains(keyword)
                || (entry.Note != null && entry.Note.ToLower().Contains(keyword))
                || entry.ChartOfAccount.AccountCode.ToLower().Contains(keyword)
                || entry.ChartOfAccount.AccountName.ToLower().Contains(keyword));
        }

        var entries = await periodQuery
            .OrderBy(entry => entry.TrxDate)
            .ThenBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.TrxNumber)
            .ThenBy(entry => entry.Id)
            .ToListAsync(ct);

        var runningBalance = openingBalance;
        var lines = entries.Select(entry =>
        {
            var movementAmount = entry.DebitAmount - entry.CreditAmount;
            runningBalance += movementAmount;

            return new AccountingCashFlowReportLineDto(
                entry.Id,
                entry.TrxDate,
                entry.TrxNumber,
                entry.ReferenceType,
                entry.ReferenceId,
                entry.ChartOfAccountId,
                entry.ChartOfAccount.AccountCode,
                entry.ChartOfAccount.AccountName,
                entry.OutletId,
                entry.Outlet?.Name,
                entry.Note,
                entry.DebitAmount,
                entry.CreditAmount,
                movementAmount,
                runningBalance
            );
        }).ToList();

        var summary = new AccountingCashFlowReportSummaryDto(
            OpeningBalance: openingBalance,
            CashIn: entries.Sum(entry => entry.DebitAmount),
            CashOut: entries.Sum(entry => entry.CreditAmount),
            ClosingBalance: openingBalance + entries.Sum(entry => entry.DebitAmount - entry.CreditAmount)
        );

        return new AccountingCashFlowReportDto(
            new AccountingCashFlowReportFilters(filters.DateFrom, filters.DateTo, outletId, filters.ChartOfAccountId, filters.Keyword?.Trim()),
            summary,
            lines);
    }

    public async Task<ExportReportResponse> ExportCashFlowExcelAsync(
        AccountingCashFlowReportFilters filters,
        CancellationToken ct = default)
    {
        var report = await GetCashFlowReportAsync(filters, ct);
        var periodStart = report.Filters.DateFrom?.Date ?? DateTime.UtcNow.Date;
        var periodEnd = report.Filters.DateTo?.Date ?? DateTime.UtcNow.Date;
        var outletLabel = report.Filters.OutletId.HasValue
            ? report.Lines.Select(line => line.OutletName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Outlet terpilih"
            : "Semua outlet";

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            AppendTextRow(sheetData, "Laporan Arus Kas MorrusPOS");
            AppendTextRow(sheetData, $"Periode: {periodStart:yyyy-MM-dd} s/d {periodEnd:yyyy-MM-dd}");
            AppendTextRow(sheetData, $"Outlet: {outletLabel}");
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "Ringkasan");
            AppendTextRow(sheetData, "Metrik", "Nilai");
            AppendTextRow(sheetData, "Kas Awal", report.Summary.OpeningBalance);
            AppendTextRow(sheetData, "Kas Masuk", report.Summary.CashIn);
            AppendTextRow(sheetData, "Kas Keluar", report.Summary.CashOut);
            AppendTextRow(sheetData, "Kas Akhir", report.Summary.ClosingBalance);
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "Mutasi Kas");
            AppendTextRow(
                sheetData,
                "Tanggal",
                "No. Transaksi",
                "Outlet",
                "Kode Akun",
                "Nama Akun",
                "Catatan",
                "Debit",
                "Kredit",
                "Mutasi",
                "Saldo Berjalan");

            foreach (var line in report.Lines)
            {
                AppendTextRow(
                    sheetData,
                    line.TrxDate.ToString("yyyy-MM-dd"),
                    line.TrxNumber,
                    line.OutletName ?? "Business",
                    line.AccountCode,
                    line.AccountName,
                    line.Note ?? string.Empty,
                    line.DebitAmount,
                    line.CreditAmount,
                    line.MovementAmount,
                    line.RunningBalance);
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Arus Kas"
            });

            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Laporan_Arus_Kas_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.xlsx");
    }

    public async Task<GeneralLedgerReportDto> GetGeneralLedgerReportAsync(
        GeneralLedgerReportFilters filters,
        CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var (startUtc, endUtc) = NormalizePeriod(filters.DateFrom, filters.DateTo);
        var outletId = await ResolveAccessibleOutletIdAsync(filters.OutletId, ct);

        if (filters.ChartOfAccountId.HasValue)
        {
            var accountExists = await _dbContext.ChartOfAccounts
                .AsNoTracking()
                .AnyAsync(account => account.Id == filters.ChartOfAccountId.Value, ct);
            if (!accountExists)
            {
                throw new InvalidOperationException("Akun filter tidak ditemukan.");
            }
        }

        var baseQuery = _dbContext.AccountTransactions
            .Include(entry => entry.ChartOfAccount)
            .Include(entry => entry.Outlet)
            .AsNoTracking()
            .Where(entry => entry.BusinessId == businessId)
            .Where(entry => entry.TrxEntity == AccountingTransactionEntity.Business);

        if (outletId.HasValue)
        {
            baseQuery = baseQuery.Where(entry => entry.OutletId == outletId.Value);
        }

        if (filters.ChartOfAccountId.HasValue)
        {
            baseQuery = baseQuery.Where(entry => entry.ChartOfAccountId == filters.ChartOfAccountId.Value);
        }

        var openingBalance = await baseQuery
            .Where(entry => entry.TrxDate < startUtc)
            .SumAsync(entry => entry.DebitAmount - entry.CreditAmount, ct);

        var periodQuery = baseQuery
            .Where(entry => entry.TrxDate >= startUtc && entry.TrxDate <= endUtc);

        if (!string.IsNullOrWhiteSpace(filters.Keyword))
        {
            var keyword = filters.Keyword.Trim().ToLowerInvariant();
            periodQuery = periodQuery.Where(entry =>
                entry.TrxNumber.ToLower().Contains(keyword)
                || (entry.Note != null && entry.Note.ToLower().Contains(keyword))
                || entry.ChartOfAccount.AccountCode.ToLower().Contains(keyword)
                || entry.ChartOfAccount.AccountName.ToLower().Contains(keyword));
        }

        var entries = await periodQuery
            .OrderBy(entry => entry.TrxDate)
            .ThenBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.TrxNumber)
            .ThenBy(entry => entry.Id)
            .ToListAsync(ct);

        var runningBalance = openingBalance;
        var lines = entries.Select(entry =>
        {
            var movementAmount = entry.DebitAmount - entry.CreditAmount;
            runningBalance += movementAmount;

            return new GeneralLedgerReportLineDto(
                entry.Id,
                entry.TrxDate,
                entry.TrxNumber,
                entry.ReferenceType,
                entry.ReferenceId,
                entry.ChartOfAccountId,
                entry.ChartOfAccount.AccountCode,
                entry.ChartOfAccount.AccountName,
                entry.ChartOfAccount.AccountType.ToString(),
                entry.OutletId,
                entry.Outlet?.Name,
                entry.Note,
                entry.DebitAmount,
                entry.CreditAmount,
                movementAmount,
                runningBalance
            );
        }).ToList();

        var summary = new GeneralLedgerReportSummaryDto(
            OpeningBalance: openingBalance,
            TotalDebit: entries.Sum(entry => entry.DebitAmount),
            TotalCredit: entries.Sum(entry => entry.CreditAmount),
            ClosingBalance: openingBalance + entries.Sum(entry => entry.DebitAmount - entry.CreditAmount)
        );

        return new GeneralLedgerReportDto(
            new GeneralLedgerReportFilters(filters.DateFrom, filters.DateTo, outletId, filters.ChartOfAccountId, filters.Keyword?.Trim()),
            summary,
            lines);
    }

    public async Task<ExportReportResponse> ExportGeneralLedgerExcelAsync(
        GeneralLedgerReportFilters filters,
        CancellationToken ct = default)
    {
        var report = await GetGeneralLedgerReportAsync(filters, ct);
        var periodStart = report.Filters.DateFrom?.Date ?? DateTime.UtcNow.Date;
        var periodEnd = report.Filters.DateTo?.Date ?? DateTime.UtcNow.Date;
        var outletLabel = report.Filters.OutletId.HasValue
            ? report.Lines.Select(line => line.OutletName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Outlet terpilih"
            : "Semua outlet";

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            AppendTextRow(sheetData, "Laporan Buku Besar (General Ledger) MorrusPOS");
            AppendTextRow(sheetData, $"Periode: {periodStart:yyyy-MM-dd} s/d {periodEnd:yyyy-MM-dd}");
            AppendTextRow(sheetData, $"Outlet: {outletLabel}");
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "Ringkasan");
            AppendTextRow(sheetData, "Metrik", "Nilai");
            AppendTextRow(sheetData, "Saldo Awal", report.Summary.OpeningBalance);
            AppendTextRow(sheetData, "Total Debit", report.Summary.TotalDebit);
            AppendTextRow(sheetData, "Total Kredit", report.Summary.TotalCredit);
            AppendTextRow(sheetData, "Saldo Akhir", report.Summary.ClosingBalance);
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "Rincian Transaksi Jurnal");
            AppendTextRow(
                sheetData,
                "Tanggal",
                "No. Jurnal",
                "Tipe Referensi",
                "Outlet",
                "Kode Akun",
                "Nama Akun",
                "Tipe Akun",
                "Catatan",
                "Debit",
                "Kredit",
                "Saldo Berjalan");

            foreach (var line in report.Lines)
            {
                AppendTextRow(
                    sheetData,
                    line.TrxDate.ToString("yyyy-MM-dd"),
                    line.TrxNumber,
                    line.ReferenceType,
                    line.OutletName ?? "Business",
                    line.AccountCode,
                    line.AccountName,
                    line.AccountType,
                    line.Note ?? string.Empty,
                    line.DebitAmount,
                    line.CreditAmount,
                    line.RunningBalance);
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Buku Besar"
            });

            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Laporan_Buku_Besar_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.xlsx");
    }

    public async Task<AccountingProfitLossReportDto> GetAccountingProfitLossReportAsync(
        AccountingProfitLossReportFilters filters,
        CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var (startUtc, endUtc) = NormalizePeriod(filters.DateFrom, filters.DateTo);
        var outletId = await ResolveAccessibleOutletIdAsync(filters.OutletId, ct);

        var query = _dbContext.AccountTransactions
            .Include(entry => entry.ChartOfAccount)
            .Include(entry => entry.Outlet)
            .AsNoTracking()
            .Where(entry => entry.BusinessId == businessId)
            .Where(entry => entry.TrxEntity == AccountingTransactionEntity.Business)
            .Where(entry => entry.TrxDate >= startUtc && entry.TrxDate <= endUtc)
            .Where(entry =>
                entry.ChartOfAccount.AccountType == ChartOfAccountType.Revenue
                || entry.ChartOfAccount.AccountType == ChartOfAccountType.Cogs
                || entry.ChartOfAccount.AccountType == ChartOfAccountType.Expense);

        if (outletId.HasValue)
        {
            query = query.Where(entry => entry.OutletId == outletId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Keyword))
        {
            var keyword = filters.Keyword.Trim().ToLowerInvariant();
            query = query.Where(entry =>
                entry.TrxNumber.ToLower().Contains(keyword)
                || (entry.Note != null && entry.Note.ToLower().Contains(keyword))
                || entry.ChartOfAccount.AccountCode.ToLower().Contains(keyword)
                || entry.ChartOfAccount.AccountName.ToLower().Contains(keyword));
        }

        var entries = await query.ToListAsync(ct);

        var revenueAccounts = BuildProfitLossSection(entries, ChartOfAccountType.Revenue);
        var cogsAccounts = BuildProfitLossSection(entries, ChartOfAccountType.Cogs);
        var expenseAccounts = BuildProfitLossSection(entries, ChartOfAccountType.Expense);

        var summary = new AccountingProfitLossReportSummaryDto(
            RevenueTotal: revenueAccounts.Total,
            CogsTotal: cogsAccounts.Total,
            ExpenseTotal: expenseAccounts.Total,
            GrossProfit: revenueAccounts.Total - cogsAccounts.Total,
            NetProfit: revenueAccounts.Total - cogsAccounts.Total - expenseAccounts.Total
        );

        return new AccountingProfitLossReportDto(
            new AccountingProfitLossReportFilters(filters.DateFrom, filters.DateTo, outletId, filters.Keyword?.Trim()),
            revenueAccounts,
            cogsAccounts,
            expenseAccounts,
            summary
        );
    }

    public async Task<ExportReportResponse> ExportAccountingProfitLossExcelAsync(
        AccountingProfitLossReportFilters filters,
        CancellationToken ct = default)
    {
        var report = await GetAccountingProfitLossReportAsync(filters, ct);
        var periodStart = report.Filters.DateFrom?.Date ?? DateTime.UtcNow.Date;
        var periodEnd = report.Filters.DateTo?.Date ?? DateTime.UtcNow.Date;
        var outletLabel = report.Filters.OutletId.HasValue
            ? "Outlet terpilih"
            : "Semua outlet";

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            AppendTextRow(sheetData, "Laporan Laba Rugi Akuntansi MorrusPOS");
            AppendTextRow(sheetData, $"Periode: {periodStart:yyyy-MM-dd} s/d {periodEnd:yyyy-MM-dd}");
            AppendTextRow(sheetData, $"Outlet: {outletLabel}");
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "Ringkasan");
            AppendTextRow(sheetData, "Metrik", "Nilai");
            AppendTextRow(sheetData, "Pendapatan", report.Summary.RevenueTotal);
            AppendTextRow(sheetData, "HPP", report.Summary.CogsTotal);
            AppendTextRow(sheetData, "Laba Kotor", report.Summary.GrossProfit);
            AppendTextRow(sheetData, "Biaya", report.Summary.ExpenseTotal);
            AppendTextRow(sheetData, "Laba Bersih", report.Summary.NetProfit);
            AppendEmptyRow(sheetData);

            AppendProfitLossSection(sheetData, "Pendapatan", report.Revenue);
            AppendProfitLossSection(sheetData, "Harga Pokok Penjualan", report.Cogs);
            AppendProfitLossSection(sheetData, "Biaya Operasional", report.Expense);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Laba Rugi"
            });

            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Laporan_Laba_Rugi_Akuntansi_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.xlsx");
    }

    public async Task<ProfitLossReportDto> GetProfitLossReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var query = _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= startUtc && t.CreatedAt <= endUtc);

        string outletName = "Semua Outlet";
        if (outletId.HasValue)
        {
            query = query.Where(t => t.OutletId == outletId.Value);
            var outlet = await _dbContext.Outlets.FindAsync(new object[] { outletId.Value }, ct);
            if (outlet != null)
            {
                outletName = outlet.Name;
            }
        }

        var transactions = await query
            .Select(t => new { t.Subtotal, t.DiscountTotal, t.TaxTotal, t.GrandTotal })
            .ToListAsync(ct);

        decimal grossRevenue = transactions.Sum(t => t.Subtotal);
        decimal totalDiscount = transactions.Sum(t => t.DiscountTotal);
        decimal totalTax = transactions.Sum(t => t.TaxTotal);
        decimal netRevenue = transactions.Sum(t => t.GrandTotal);

        var itemCostsQuery = _dbContext.TransactionItems
            .AsNoTracking()
            .Where(ti => ti.Transaction.Status == TransactionStatus.Completed 
                         && ti.Transaction.CreatedAt >= startUtc 
                         && ti.Transaction.CreatedAt <= endUtc);

        if (outletId.HasValue)
        {
            itemCostsQuery = itemCostsQuery.Where(ti => ti.Transaction.OutletId == outletId.Value);
        }

        var itemCosts = await itemCostsQuery
            .Select(ti => new {
                CategoryId = ti.Product.CategoryId,
                CategoryName = ti.Product.Category != null ? ti.Product.Category.Name : "Tanpa Kategori",
                Revenue = ti.LineTotal,
                Cost = ti.Qty * ti.UnitCost
            })
            .ToListAsync(ct);

        decimal costOfGoodsSold = itemCosts.Sum(ti => ti.Cost);
        decimal grossProfit = netRevenue - costOfGoodsSold;

        var categoryBreakdown = itemCosts
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(g => new ProfitLossCategorySummaryDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Sum(x => x.Revenue),
                g.Sum(x => x.Cost),
                g.Sum(x => x.Revenue - x.Cost)
            ))
            .OrderByDescending(x => x.Revenue)
            .ToList();

        return new ProfitLossReportDto(
            startDate,
            endDate,
            outletId,
            outletName,
            grossRevenue,
            totalDiscount,
            totalTax,
            netRevenue,
            costOfGoodsSold,
            grossProfit,
            categoryBreakdown
        );
    }

    public async Task<ExportReportResponse> ExportProfitLossExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var report = await GetProfitLossReportAsync(outletId, startDate, endDate, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Laporan Laba Rugi MorrusPOS");
        sb.AppendLine($"Periode:;{report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Outlet:;{report.OutletName}");
        sb.AppendLine();

        sb.AppendLine("RINGKASAN FINANSIAL");
        sb.AppendLine("Metrik;Nilai");
        sb.AppendLine($"Pendapatan Kotor (Gross Revenue);{report.GrossRevenue:F2}");
        sb.AppendLine($"Total Diskon;{report.TotalDiscount:F2}");
        sb.AppendLine($"Total Pajak;{report.TotalTax:F2}");
        sb.AppendLine($"Pendapatan Bersih (Net Revenue);{report.NetRevenue:F2}");
        sb.AppendLine($"Harga Pokok Penjualan (HPP / COGS);{report.CostOfGoodsSold:F2}");
        sb.AppendLine($"Laba Kotor (Gross Profit);{report.GrossProfit:F2}");
        sb.AppendLine();

        sb.AppendLine("RINCIAN PER KATEGORI");
        sb.AppendLine("Kategori;Pendapatan;HPP;Laba Kotor");
        foreach (var cat in report.CategoryBreakdown)
        {
            sb.AppendLine($"{cat.CategoryName};{cat.Revenue:F2};{cat.CostOfGoodsSold:F2};{cat.GrossProfit:F2}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Laporan_Laba_Rugi_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.csv";

        return new ExportReportResponse(csvBytes, "text/csv", fileName);
    }

    public async Task<PurchaseRecapReportDto> GetPurchaseRecapReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var poQuery = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.Status == "completed" && p.CreatedAt >= startUtc && p.CreatedAt <= endUtc);

        string outletName = "Semua Outlet";
        if (outletId.HasValue)
        {
            poQuery = poQuery.Where(p => p.OutletId == outletId.Value);
            var outlet = await _dbContext.Outlets.FindAsync(new object[] { outletId.Value }, ct);
            if (outlet != null)
            {
                outletName = outlet.Name;
            }
        }

        var pos = await poQuery
            .Select(p => new { p.Id, p.TotalAmount, p.SupplierId, SupplierName = p.Supplier.Name })
            .ToListAsync(ct);

        decimal totalSpent = pos.Sum(p => p.TotalAmount);
        int totalOrdersCount = pos.Count;

        var poIds = pos.Select(p => p.Id).ToList();

        var itemsQuery = _dbContext.PurchaseOrderItems
            .AsNoTracking()
            .Where(pi => poIds.Contains(pi.PurchaseOrderId));

        var items = await itemsQuery
            .Select(pi => new {
                pi.ProductId,
                ProductName = pi.Product.Name,
                ProductSku = pi.Product.Sku,
                pi.Qty,
                pi.UnitCost,
                LineTotal = pi.Qty * pi.UnitCost
            })
            .ToListAsync(ct);

        var productBreakdown = items
            .GroupBy(x => new { x.ProductId, x.ProductName, x.ProductSku })
            .Select(g => {
                var totalQty = g.Sum(x => x.Qty);
                var totalProductSpent = g.Sum(x => x.LineTotal);
                var avgUnitCost = totalQty > 0 ? totalProductSpent / totalQty : 0;
                return new PurchaseProductSummaryDto(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.ProductSku,
                    totalQty,
                    avgUnitCost,
                    totalProductSpent
                );
            })
            .OrderByDescending(x => x.TotalSpent)
            .ToList();

        var supplierBreakdown = pos
            .GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(g => new PurchaseSupplierSummaryDto(
                g.Key.SupplierId,
                g.Key.SupplierName,
                g.Count(),
                g.Sum(x => x.TotalAmount)
            ))
            .OrderByDescending(x => x.TotalSpent)
            .ToList();

        return new PurchaseRecapReportDto(
            startDate,
            endDate,
            outletId,
            outletName,
            totalSpent,
            totalOrdersCount,
            productBreakdown,
            supplierBreakdown
        );
    }

    public async Task<ExportReportResponse> ExportPurchaseRecapExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var report = await GetPurchaseRecapReportAsync(outletId, startDate, endDate, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Laporan Rekap Pembelian MorrusPOS");
        sb.AppendLine($"Periode:;{report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Outlet:;{report.OutletName}");
        sb.AppendLine();

        sb.AppendLine("RINGKASAN PEMBELIAN");
        sb.AppendLine("Metrik;Nilai");
        sb.AppendLine($"Total Pengeluaran Belanja;{report.TotalSpent:F2}");
        sb.AppendLine($"Total Dokumen PO Selesai;{report.TotalOrdersCount}");
        sb.AppendLine();

        sb.AppendLine("RINCIAN PEMBELIAN PER PRODUK");
        sb.AppendLine("SKU;Nama Produk;Total Qty Belanja;Harga Rata-Rata Beli;Total Belanja");
        foreach (var p in report.ProductBreakdown)
        {
            sb.AppendLine($"{p.Sku};{p.ProductName};{p.TotalQty:F2};{p.AverageUnitCost:F2};{p.TotalSpent:F2}");
        }
        sb.AppendLine();

        sb.AppendLine("RINCIAN BELANJA PER SUPPLIER");
        sb.AppendLine("Nama Supplier;Total Dokumen PO;Total Belanja");
        foreach (var s in report.SupplierBreakdown)
        {
            sb.AppendLine($"{s.SupplierName};{s.TotalOrders};{s.TotalSpent:F2}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Rekap_Pembelian_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.csv";

        return new ExportReportResponse(csvBytes, "text/csv", fileName);
    }

    public async Task<SalesRecapReportDto> GetSalesRecapReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var startUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var txQuery = _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed && t.CreatedAt >= startUtc && t.CreatedAt <= endUtc);

        string outletName = "Semua Outlet";
        if (outletId.HasValue)
        {
            txQuery = txQuery.Where(t => t.OutletId == outletId.Value);
            var outlet = await _dbContext.Outlets.FindAsync(new object[] { outletId.Value }, ct);
            if (outlet != null)
            {
                outletName = outlet.Name;
            }
        }

        var transactions = await txQuery
            .Select(t => new { t.Id, t.Subtotal, t.DiscountTotal, t.TaxTotal, t.GrandTotal })
            .ToListAsync(ct);

        decimal grossRevenue = transactions.Sum(t => t.Subtotal);
        decimal totalDiscount = transactions.Sum(t => t.DiscountTotal);
        decimal totalTax = transactions.Sum(t => t.TaxTotal);
        decimal netRevenue = transactions.Sum(t => t.GrandTotal);

        var txIds = transactions.Select(t => t.Id).ToList();

        var itemsQuery = _dbContext.TransactionItems
            .AsNoTracking()
            .Where(ti => txIds.Contains(ti.TransactionId));

        var items = await itemsQuery
            .Select(ti => new {
                ti.ProductId,
                ProductName = ti.Product.Name,
                ProductSku = ti.Product.Sku,
                ti.Qty,
                ti.UnitPrice,
                ti.LineTotal,
                ti.UnitCost
            })
            .ToListAsync(ct);

        var productBreakdown = items
            .GroupBy(x => new { x.ProductId, x.ProductName, x.ProductSku })
            .Select(g => {
                var totalQty = g.Sum(x => x.Qty);
                var totalRevenue = g.Sum(x => x.LineTotal);
                var totalCost = g.Sum(x => x.Qty * x.UnitCost);
                var totalProfit = totalRevenue - totalCost;
                return new SalesProductSummaryDto(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.ProductSku,
                    totalQty,
                    totalRevenue,
                    totalCost,
                    totalProfit
                );
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        decimal costOfGoodsSold = productBreakdown.Sum(x => x.TotalCostOfGoodsSold);
        decimal grossProfit = netRevenue - costOfGoodsSold;

        var paymentsQuery = _dbContext.Payments
            .AsNoTracking()
            .Where(p => txIds.Contains(p.TransactionId));

        var payments = await paymentsQuery
            .Select(p => new { PaymentMethod = p.Method, p.Amount })
            .ToListAsync(ct);

        var paymentBreakdown = payments
            .GroupBy(x => x.PaymentMethod)
            .Select(g => new SalesPaymentSummaryDto(
                string.IsNullOrWhiteSpace(g.Key) ? "Lainnya" : g.Key,
                g.Count(),
                g.Sum(x => x.Amount)
            ))
            .OrderByDescending(x => x.TotalCollected)
            .ToList();

        return new SalesRecapReportDto(
            startDate,
            endDate,
            outletId,
            outletName,
            grossRevenue,
            totalDiscount,
            netRevenue,
            costOfGoodsSold,
            grossProfit,
            productBreakdown,
            paymentBreakdown
        );
    }

    public async Task<ExportReportResponse> ExportSalesRecapExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var report = await GetSalesRecapReportAsync(outletId, startDate, endDate, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Laporan Rekap Penjualan MorrusPOS");
        sb.AppendLine($"Periode:;{report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Outlet:;{report.OutletName}");
        sb.AppendLine();

        sb.AppendLine("RINGKASAN PENJUALAN");
        sb.AppendLine("Metrik;Nilai");
        sb.AppendLine($"Pendapatan Kotor (Gross Revenue);{report.GrossRevenue:F2}");
        sb.AppendLine($"Total Diskon;{report.TotalDiscount:F2}");
        sb.AppendLine($"Pendapatan Bersih (Net Revenue);{report.NetRevenue:F2}");
        sb.AppendLine($"Harga Pokok Penjualan (HPP / COGS);{report.CostOfGoodsSold:F2}");
        sb.AppendLine($"Laba Kotor (Gross Profit);{report.GrossProfit:F2}");
        sb.AppendLine();

        sb.AppendLine("RINCIAN PENJUALAN PER PRODUK");
        sb.AppendLine("SKU;Nama Produk;Qty Terjual;Total Omzet;Total HPP;Total Laba");
        foreach (var p in report.ProductBreakdown)
        {
            sb.AppendLine($"{p.Sku};{p.ProductName};{p.TotalQty:F2};{p.TotalRevenue:F2};{p.TotalCostOfGoodsSold:F2};{p.TotalGrossProfit:F2}");
        }
        sb.AppendLine();

        sb.AppendLine("RINCIAN PENERIMAAN PER METODE PEMBAYARAN");
        sb.AppendLine("Metode Pembayaran;Total Transaksi;Total Diterima");
        foreach (var pay in report.PaymentBreakdown)
        {
            sb.AppendLine($"{pay.PaymentMethod};{pay.TransactionCount};{pay.TotalCollected:F2}");
        }

        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Rekap_Penjualan_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.csv";

        return new ExportReportResponse(csvBytes, "text/csv", fileName);
    }

    private AccountingProfitLossSectionDto BuildProfitLossSection(
        IReadOnlyCollection<AccountTransaction> entries,
        string accountType)
    {
        var accounts = entries
            .Where(entry => entry.ChartOfAccount.AccountType == accountType)
            .GroupBy(entry => new
            {
                entry.ChartOfAccountId,
                entry.ChartOfAccount.AccountCode,
                entry.ChartOfAccount.AccountName,
                entry.ChartOfAccount.AccountType
            })
            .Select(group =>
            {
                var amount = accountType == ChartOfAccountType.Revenue
                    ? group.Sum(entry => entry.CreditAmount - entry.DebitAmount)
                    : group.Sum(entry => entry.DebitAmount - entry.CreditAmount);

                return new AccountingProfitLossAccountLineDto(
                    group.Key.ChartOfAccountId,
                    group.Key.AccountCode,
                    group.Key.AccountName,
                    group.Key.AccountType,
                    amount
                );
            })
            .OrderBy(account => account.AccountCode)
            .ToList();

        return new AccountingProfitLossSectionDto(
            accountType,
            accounts.Sum(account => account.Amount),
            accounts);
    }

    private Guid EnsureBusinessContext()
    {
        if (_currentUserService?.BusinessId.HasValue != true)
        {
            throw new UnauthorizedAccessException("Business context tidak ditemukan.");
        }

        return _currentUserService.BusinessId!.Value;
    }

    private (DateTime StartUtc, DateTime EndUtc) NormalizePeriod(DateTime? dateFrom, DateTime? dateTo)
    {
        if (!dateFrom.HasValue)
        {
            throw new InvalidOperationException("Tanggal mulai wajib diisi.");
        }

        if (!dateTo.HasValue)
        {
            throw new InvalidOperationException("Tanggal akhir wajib diisi.");
        }

        if (dateTo.Value.Date < dateFrom.Value.Date)
        {
            throw new InvalidOperationException("Tanggal akhir tidak boleh sebelum tanggal mulai.");
        }

        var startUtc = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        return (startUtc, endUtc);
    }

    private async Task<Guid?> ResolveAccessibleOutletIdAsync(Guid? requestedOutletId, CancellationToken ct)
    {
        if (_currentUserService?.BusinessId.HasValue != true)
        {
            throw new UnauthorizedAccessException("Business context tidak ditemukan.");
        }

        var isPrivilegedRole = _currentUserService.Role is "Owner" or "Admin" or "Keuangan";
        var effectiveOutletId = requestedOutletId;

        if (!isPrivilegedRole)
        {
            if (_currentUserService.OutletId.HasValue)
            {
                if (requestedOutletId.HasValue && requestedOutletId != _currentUserService.OutletId.Value)
                {
                    throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
                }

                effectiveOutletId = _currentUserService.OutletId.Value;
            }
            else
            {
                effectiveOutletId = null;
            }
        }

        if (!effectiveOutletId.HasValue)
        {
            return null;
        }

        var outletExists = await _dbContext.Outlets
            .AsNoTracking()
            .AnyAsync(outlet => outlet.Id == effectiveOutletId.Value, ct);
        if (!outletExists)
        {
            throw new InvalidOperationException("Outlet tidak ditemukan.");
        }

        return effectiveOutletId;
    }

    private static void AppendEmptyRow(SheetData sheetData)
    {
        sheetData.AppendChild(new Row());
    }

    private static void AppendTextRow(SheetData sheetData, params object[] values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.AppendChild(CreateCell(value));
        }

        sheetData.AppendChild(row);
    }

    private static void AppendProfitLossSection(
        SheetData sheetData,
        string title,
        AccountingProfitLossSectionDto section)
    {
        AppendTextRow(sheetData, title);
        AppendTextRow(sheetData, "Kode Akun", "Nama Akun", "Nominal");

        foreach (var account in section.Accounts)
        {
            AppendTextRow(sheetData, account.AccountCode, account.AccountName, account.Amount);
        }

        AppendTextRow(sheetData, "Total", string.Empty, section.Total);
        AppendEmptyRow(sheetData);
    }

    private static Cell CreateCell(object? value)
    {
        return value switch
        {
            null => new Cell { DataType = CellValues.String, CellValue = new CellValue(string.Empty) },
            decimal decimalValue => new Cell { DataType = CellValues.Number, CellValue = new CellValue(decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
            int intValue => new Cell { DataType = CellValues.Number, CellValue = new CellValue(intValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
            long longValue => new Cell { DataType = CellValues.Number, CellValue = new CellValue(longValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
            double doubleValue => new Cell { DataType = CellValues.Number, CellValue = new CellValue(doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
            float floatValue => new Cell { DataType = CellValues.Number, CellValue = new CellValue(floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
            DateTime dateTimeValue => new Cell { DataType = CellValues.String, CellValue = new CellValue(dateTimeValue.ToString("yyyy-MM-dd")) },
            _ => new Cell { DataType = CellValues.String, CellValue = new CellValue(value.ToString() ?? string.Empty) }
        };
    }
}
