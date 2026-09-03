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
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            AppendTextRow(sheetData, "Laporan Laba Rugi MorrusPOS");
            AppendTextRow(sheetData, $"Periode: {report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
            AppendTextRow(sheetData, $"Outlet: {report.OutletName}");
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINGKASAN FINANSIAL");
            AppendTextRow(sheetData, "Metrik", "Nilai");
            AppendTextRow(sheetData, "Pendapatan Kotor (Gross Revenue)", report.GrossRevenue);
            AppendTextRow(sheetData, "Total Diskon", report.TotalDiscount);
            AppendTextRow(sheetData, "Total Pajak", report.TotalTax);
            AppendTextRow(sheetData, "Pendapatan Bersih (Net Revenue)", report.NetRevenue);
            AppendTextRow(sheetData, "Harga Pokok Penjualan (HPP / COGS)", report.CostOfGoodsSold);
            AppendTextRow(sheetData, "Laba Kotor (Gross Profit)", report.GrossProfit);
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINCIAN PER KATEGORI");
            AppendTextRow(sheetData, "Kategori", "Pendapatan", "HPP", "Laba Kotor");
            foreach (var cat in report.CategoryBreakdown)
            {
                AppendTextRow(sheetData, cat.CategoryName, cat.Revenue, cat.CostOfGoodsSold, cat.GrossProfit);
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Laba Rugi" });
            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Laporan_Laba_Rugi_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.xlsx");
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
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            AppendTextRow(sheetData, "Laporan Rekap Pembelian MorrusPOS");
            AppendTextRow(sheetData, $"Periode: {report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
            AppendTextRow(sheetData, $"Outlet: {report.OutletName}");
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINGKASAN PEMBELIAN");
            AppendTextRow(sheetData, "Metrik", "Nilai");
            AppendTextRow(sheetData, "Total Pengeluaran Belanja", report.TotalSpent);
            AppendTextRow(sheetData, "Total Dokumen PO Selesai", report.TotalOrdersCount);
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINCIAN PEMBELIAN PER PRODUK");
            AppendTextRow(sheetData, "SKU", "Nama Produk", "Total Qty Belanja", "Harga Rata-Rata Beli", "Total Belanja");
            foreach (var p in report.ProductBreakdown)
            {
                AppendTextRow(sheetData, p.Sku, p.ProductName, p.TotalQty, p.AverageUnitCost, p.TotalSpent);
            }
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINCIAN BELANJA PER SUPPLIER");
            AppendTextRow(sheetData, "Nama Supplier", "Total Dokumen PO", "Total Belanja");
            foreach (var s in report.SupplierBreakdown)
            {
                AppendTextRow(sheetData, s.SupplierName, s.TotalOrders, s.TotalSpent);
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Rekap Pembelian" });
            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Rekap_Pembelian_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.xlsx");
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
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            AppendTextRow(sheetData, "Laporan Rekap Penjualan MorrusPOS");
            AppendTextRow(sheetData, $"Periode: {report.StartDate:yyyy-MM-dd} s/d {report.EndDate:yyyy-MM-dd}");
            AppendTextRow(sheetData, $"Outlet: {report.OutletName}");
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINGKASAN PENJUALAN");
            AppendTextRow(sheetData, "Metrik", "Nilai");
            AppendTextRow(sheetData, "Pendapatan Kotor (Gross Revenue)", report.GrossRevenue);
            AppendTextRow(sheetData, "Total Diskon", report.TotalDiscount);
            AppendTextRow(sheetData, "Pendapatan Bersih (Net Revenue)", report.NetRevenue);
            AppendTextRow(sheetData, "Harga Pokok Penjualan (HPP / COGS)", report.CostOfGoodsSold);
            AppendTextRow(sheetData, "Laba Kotor (Gross Profit)", report.GrossProfit);
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINCIAN PENJUALAN PER PRODUK");
            AppendTextRow(sheetData, "SKU", "Nama Produk", "Qty Terjual", "Total Omzet", "Total HPP", "Total Laba");
            foreach (var p in report.ProductBreakdown)
            {
                AppendTextRow(sheetData, p.Sku, p.ProductName, p.TotalQty, p.TotalRevenue, p.TotalCostOfGoodsSold, p.TotalGrossProfit);
            }
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "RINCIAN PENERIMAAN PER METODE PEMBAYARAN");
            AppendTextRow(sheetData, "Metode Pembayaran", "Total Transaksi", "Total Diterima");
            foreach (var pay in report.PaymentBreakdown)
            {
                AppendTextRow(sheetData, pay.PaymentMethod, pay.TransactionCount, pay.TotalCollected);
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Rekap Penjualan" });
            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Rekap_Penjualan_{report.StartDate:yyyyMMdd}_{report.EndDate:yyyyMMdd}.xlsx");
    }

    public async Task<SupplierReportDto> GetSupplierReportAsync(
        SupplierReportFilters filters,
        CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var (startUtc, endUtc) = NormalizePeriod(filters.DateFrom, filters.DateTo);
        var outletId = await ResolveAccessibleOutletIdAsync(filters.OutletId, ct);

        if (!filters.SupplierId.HasValue)
        {
            throw new InvalidOperationException("Supplier wajib dipilih.");
        }

        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == filters.SupplierId.Value, ct);
        if (supplier == null)
        {
            throw new InvalidOperationException("Supplier tidak ditemukan.");
        }

        var effectiveFilters = new SupplierReportFilters(filters.DateFrom, filters.DateTo, filters.SupplierId, outletId);

        // Purchases
        var purchaseQuery = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Outlet)
            .Include(po => po.Supplier)
            .Where(po => po.SupplierId == filters.SupplierId.Value && po.PoDate >= startUtc && po.PoDate <= endUtc);
        if (outletId.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(po => po.OutletId == outletId.Value);
        }
        var purchaseOrders = await purchaseQuery.OrderBy(po => po.PoDate).ToListAsync(ct);
        var purchases = purchaseOrders
            .Where(po => po.Status == PurchaseOrderStatus.Completed)
            .Select(po => new SupplierReportPurchaseDto(
                po.Id, po.PoNumber, po.PoDate, po.OutletId, po.Outlet?.Name ?? string.Empty, po.Status, po.PaymentType, po.TotalAmount, po.DueDate))
            .ToList();

        var totalPurchase = purchases.Sum(p => p.TotalAmount);

        // Purchase Returns
        var returnQuery = _dbContext.SupplierReturns
            .AsNoTracking()
            .Include(sr => sr.PurchaseOrder).ThenInclude(po => po.Outlet)
            .Where(sr => sr.SupplierId == filters.SupplierId.Value && sr.ReturnDate >= startUtc && sr.ReturnDate <= endUtc);
        if (outletId.HasValue)
        {
            returnQuery = returnQuery.Where(sr => sr.PurchaseOrder.OutletId == outletId.Value);
        }
        var supplierReturns = await returnQuery.OrderBy(sr => sr.ReturnDate).ToListAsync(ct);
        var purchaseReturns = supplierReturns
            .Where(sr => sr.Status == SupplierReturnStatus.Completed)
            .Select(sr => new SupplierReportPurchaseReturnDto(
                sr.Id, sr.ReturnNumber, sr.ReturnDate, sr.Status, sr.TotalAmount, sr.PurchaseOrderId, sr.PurchaseOrder?.PoNumber ?? string.Empty, sr.PurchaseOrder?.Outlet?.Name))
            .ToList();
        var totalPurchaseReturn = purchaseReturns.Sum(r => r.TotalAmount);
        var netPurchase = totalPurchase - totalPurchaseReturn;

        // Debts & Payments
        var debtQuery = _dbContext.SupplierDebts
            .AsNoTracking()
            .Include(d => d.PurchaseOrder).ThenInclude(po => po.Outlet)
            .Where(d => d.SupplierId == filters.SupplierId.Value);
        if (outletId.HasValue)
        {
            debtQuery = debtQuery.Where(d => d.PurchaseOrder.OutletId == outletId.Value);
        }
        var debts = await debtQuery.ToListAsync(ct);
        var debtDtos = debts.Select(d => new SupplierReportDebtDto(
            d.Id, d.PurchaseOrderId, d.PurchaseOrder?.PoNumber ?? string.Empty, d.DueDate, d.Amount, d.PaidAmount, d.RemainingAmount, d.Status, d.PurchaseOrder?.Outlet?.Name)).ToList();

        var paymentQuery = _dbContext.SupplierPayments
            .AsNoTracking()
            .Include(p => p.PurchaseOrder).ThenInclude(po => po.Outlet)
            .Where(p => p.SupplierId == filters.SupplierId.Value && p.PaymentDate >= startUtc && p.PaymentDate <= endUtc);
        if (outletId.HasValue)
        {
            paymentQuery = paymentQuery.Where(p => p.PurchaseOrder.OutletId == outletId.Value);
        }
        var payments = await paymentQuery.OrderBy(p => p.PaymentDate).ToListAsync(ct);
        var paymentDtos = payments
            .Where(p => p.Status == SupplierPaymentStatus.Paid)
            .Select(p => new SupplierReportPaymentDto(
                p.Id, p.PurchaseOrderId, p.PurchaseOrder?.PoNumber ?? string.Empty, p.PaymentDate, p.Amount, p.PaymentMethod, p.ReferenceNumber, p.Status, p.PurchaseOrder?.Outlet?.Name)).ToList();

        var totalDebtPayment = paymentDtos.Sum(p => p.Amount);
        var outstandingDebt = debts.Sum(d => d.RemainingAmount);

        // Consignments Received
        var consignmentQuery = _dbContext.Consignments
            .AsNoTracking()
            .Include(c => c.Outlet)
            .Include(c => c.Items)
            .Where(c => c.SupplierId == filters.SupplierId.Value && c.ReceiveDate >= startUtc && c.ReceiveDate <= endUtc);
        if (outletId.HasValue)
        {
            consignmentQuery = consignmentQuery.Where(c => c.OutletId == outletId.Value);
        }
        var consignments = await consignmentQuery.OrderBy(c => c.ReceiveDate).ToListAsync(ct);
        var consignmentDtos = consignments
            .Where(c => c.Status == ConsignmentStatus.Received)
            .Select(c => new SupplierReportConsignmentDto(
                c.Id, c.ConsignmentNumber, c.ReceiveDate, c.Status, c.Items.Sum(i => i.Qty * i.UnitCost), c.Items.Count, c.Outlet?.Name ?? string.Empty))
            .ToList();
        var consignmentReceivedValue = consignmentDtos.Sum(c => c.TotalValue);

        // Consignment Returns
        var consignmentReturnQuery = _dbContext.ConsignmentReturns
            .AsNoTracking()
            .Include(cr => cr.Outlet)
            .Include(cr => cr.Items)
            .Where(cr => cr.SupplierId == filters.SupplierId.Value && cr.ReturnDate >= startUtc && cr.ReturnDate <= endUtc);
        if (outletId.HasValue)
        {
            consignmentReturnQuery = consignmentReturnQuery.Where(cr => cr.OutletId == outletId.Value);
        }
        var consignmentReturns = await consignmentReturnQuery.OrderBy(cr => cr.ReturnDate).ToListAsync(ct);
        // For valuation, multiply qty by product cost price fallback
        var consignmentReturnDtos = new List<SupplierReportConsignmentReturnDto>();
        foreach (var cr in consignmentReturns.Where(c => c.Status == ConsignmentReturnStatus.Completed))
        {
            var totalQty = cr.Items.Sum(i => i.Qty);
            consignmentReturnDtos.Add(new SupplierReportConsignmentReturnDto(
                cr.Id, cr.ReturnNumber, cr.ReturnDate, cr.Status, totalQty, cr.Items.Count, cr.Outlet?.Name ?? string.Empty));
        }
        // Compute return value via cost lookup
        decimal consignmentReturnValue = 0;
        foreach (var cr in consignmentReturns.Where(c => c.Status == ConsignmentReturnStatus.Completed))
        {
            foreach (var item in cr.Items)
            {
                var product = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
                var unitCost = product?.CostPrice ?? 0;
                consignmentReturnValue += item.Qty * unitCost;
            }
        }

        // Consignment Sales
        var consignmentSalesQuery = _dbContext.ConsignmentSales
            .AsNoTracking()
            .Include(cs => cs.TransactionItem).ThenInclude(ti => ti.Transaction).ThenInclude(t => t.Outlet)
            .Include(cs => cs.TransactionItem).ThenInclude(ti => ti.Product)
            .Include(cs => cs.Supplier)
            .Where(cs => cs.SupplierId == filters.SupplierId.Value && cs.CreatedAt >= startUtc && cs.CreatedAt <= endUtc);
        if (outletId.HasValue)
        {
            consignmentSalesQuery = consignmentSalesQuery.Where(cs => cs.TransactionItem.Transaction.OutletId == outletId.Value);
        }
        var consignmentSales = await consignmentSalesQuery.OrderBy(cs => cs.CreatedAt).ToListAsync(ct);
        var consignmentSalesDtos = consignmentSales.Select(cs => new SupplierReportConsignmentSaleDto(
            cs.Id,
            cs.TransactionItem?.Transaction?.TransactionNumber ?? string.Empty,
            cs.CreatedAt,
            cs.TransactionItem?.Product?.Name ?? string.Empty,
            cs.Qty,
            cs.UnitCost,
            cs.TotalAmount,
            cs.Status,
            cs.TransactionItem?.Transaction?.Outlet?.Name ?? string.Empty)).ToList();
        var consignmentSalesValue = consignmentSales.Sum(cs => cs.TotalAmount);

        // Settlements
        var settlementQuery = _dbContext.ConsignmentSettlements
            .AsNoTracking()
            .Include(s => s.Outlet)
            .Include(s => s.Sales)
            .Where(s => s.SupplierId == filters.SupplierId.Value && s.SettlementDate >= startUtc && s.SettlementDate <= endUtc);
        if (outletId.HasValue)
        {
            settlementQuery = settlementQuery.Where(s => s.OutletId == outletId.Value);
        }
        var settlements = await settlementQuery.OrderBy(s => s.SettlementDate).ToListAsync(ct);
        var settlementDtos = settlements
            .Where(s => s.Status == ConsignmentSettlementStatus.Settled)
            .Select(s => new SupplierReportSettlementDto(
                s.Id, s.SettlementNumber, s.SettlementDate, s.TotalAmount, s.Status, s.Sales.Count, s.Outlet?.Name ?? string.Empty))
            .ToList();
        var settlementValue = settlementDtos.Sum(s => s.TotalAmount);

        var supplierDto = new SupplierReportSupplierDto(
            supplier.Id, supplier.Name, supplier.Phone, supplier.Email, supplier.Address, supplier.IsActive);

        var summary = new SupplierReportSummaryDto(
            totalPurchase,
            totalPurchaseReturn,
            netPurchase,
            totalDebtPayment,
            outstandingDebt,
            consignmentReceivedValue,
            consignmentReturnValue,
            consignmentSalesValue,
            settlementValue);

        return new SupplierReportDto(
            effectiveFilters,
            supplierDto,
            summary,
            purchases,
            purchaseReturns,
            debtDtos,
            paymentDtos,
            consignmentDtos,
            consignmentReturnDtos,
            consignmentSalesDtos,
            settlementDtos);
    }

    public async Task<ExportReportResponse> ExportSupplierReportExcelAsync(
        SupplierReportFilters filters,
        CancellationToken ct = default)
    {
        var report = await GetSupplierReportAsync(filters, ct);
        var periodStart = report.Filters.DateFrom?.Date ?? DateTime.UtcNow.Date;
        var periodEnd = report.Filters.DateTo?.Date ?? DateTime.UtcNow.Date;
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            // Sheet 1: Ringkasan
            var wsSummary = workbookPart.AddNewPart<WorksheetPart>();
            var sdSummary = new SheetData();
            wsSummary.Worksheet = new Worksheet(sdSummary);
            AppendTextRow(sdSummary, "Laporan Supplier 360 MorrusPOS");
            AppendTextRow(sdSummary, $"Supplier: {report.Supplier?.SupplierName ?? "-"}");
            AppendTextRow(sdSummary, $"Periode: {periodStart:yyyy-MM-dd} s/d {periodEnd:yyyy-MM-dd}");
            AppendTextRow(sdSummary, $"Outlet: {(report.Filters.OutletId.HasValue ? report.Purchases.FirstOrDefault()?.OutletName ?? report.Consignments.FirstOrDefault()?.OutletName ?? "Outlet terpilih" : "Semua outlet")}");
            AppendEmptyRow(sdSummary);
            AppendTextRow(sdSummary, "Ringkasan");
            AppendTextRow(sdSummary, "Metrik", "Nilai");
            AppendTextRow(sdSummary, "Total Pembelian", report.Summary.TotalPurchase);
            AppendTextRow(sdSummary, "Retur Pembelian", report.Summary.TotalPurchaseReturn);
            AppendTextRow(sdSummary, "Pembelian Bersih", report.Summary.NetPurchase);
            AppendTextRow(sdSummary, "Total Pembayaran Hutang", report.Summary.TotalDebtPayment);
            AppendTextRow(sdSummary, "Saldo Hutang Akhir", report.Summary.OutstandingDebt);
            AppendTextRow(sdSummary, "Nilai Barang Konsinyasi Diterima", report.Summary.ConsignmentReceivedValue);
            AppendTextRow(sdSummary, "Return Konsinyasi", report.Summary.ConsignmentReturnValue);
            AppendTextRow(sdSummary, "Nilai Penjualan Konsinyasi", report.Summary.ConsignmentSalesValue);
            AppendTextRow(sdSummary, "Nilai Settlement", report.Summary.SettlementValue);
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(wsSummary), SheetId = 1, Name = "Ringkasan" });

            // Sheet 2: Pembelian
            var wsPurchases = workbookPart.AddNewPart<WorksheetPart>();
            var sdPurchases = new SheetData();
            wsPurchases.Worksheet = new Worksheet(sdPurchases);
            AppendTextRow(sdPurchases, "Pembelian");
            AppendTextRow(sdPurchases, "No. PO", "Tanggal", "Outlet", "Status", "Tipe Bayar", "Total", "Jatuh Tempo");
            foreach (var p in report.Purchases)
            {
                AppendTextRow(sdPurchases, p.PoNumber, p.PoDate.ToString("yyyy-MM-dd"), p.OutletName, p.Status, p.PaymentType, p.TotalAmount, p.DueDate?.ToString("yyyy-MM-dd") ?? "-");
            }
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(wsPurchases), SheetId = 2, Name = "Pembelian" });

            // Sheet 3: Retur Pembelian
            var wsReturns = workbookPart.AddNewPart<WorksheetPart>();
            var sdReturns = new SheetData();
            wsReturns.Worksheet = new Worksheet(sdReturns);
            AppendTextRow(sdReturns, "Retur Pembelian");
            AppendTextRow(sdReturns, "No. Retur", "Tanggal", "Status", "Total", "No. PO", "Outlet");
            foreach (var r in report.PurchaseReturns)
            {
                AppendTextRow(sdReturns, r.ReturnNumber, r.ReturnDate.ToString("yyyy-MM-dd"), r.Status, r.TotalAmount, r.PoNumber, r.OutletName ?? "-");
            }
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(wsReturns), SheetId = 3, Name = "Retur Pembelian" });

            // Sheet 4: Hutang & Pembayaran
            var wsDebt = workbookPart.AddNewPart<WorksheetPart>();
            var sdDebt = new SheetData();
            wsDebt.Worksheet = new Worksheet(sdDebt);
            AppendTextRow(sdDebt, "Hutang");
            AppendTextRow(sdDebt, "No. PO", "Jatuh Tempo", "Nilai Hutang", "Terbayar", "Sisa", "Status", "Outlet");
            foreach (var d in report.Debts)
            {
                AppendTextRow(sdDebt, d.PoNumber, d.DueDate.ToString("yyyy-MM-dd"), d.Amount, d.PaidAmount, d.RemainingAmount, d.Status, d.OutletName ?? "-");
            }
            AppendEmptyRow(sdDebt);
            AppendTextRow(sdDebt, "Pembayaran");
            AppendTextRow(sdDebt, "Tanggal", "No. PO", "Metode", "Nominal", "Referensi", "Status", "Outlet");
            foreach (var p in report.Payments)
            {
                AppendTextRow(sdDebt, p.PaymentDate.ToString("yyyy-MM-dd"), p.PoNumber, p.PaymentMethod, p.Amount, p.ReferenceNumber ?? "-", p.Status, p.OutletName ?? "-");
            }
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(wsDebt), SheetId = 4, Name = "Hutang_Pembayaran" });

            // Sheet 5: Konsinyasi & Settlement
            var wsCons = workbookPart.AddNewPart<WorksheetPart>();
            var sdCons = new SheetData();
            wsCons.Worksheet = new Worksheet(sdCons);
            AppendTextRow(sdCons, "Konsinyasi Masuk (Ambil)");
            AppendTextRow(sdCons, "No. Konsinyasi", "Tanggal", "Status", "Nilai", "Jml Item", "Outlet");
            foreach (var c in report.Consignments)
            {
                AppendTextRow(sdCons, c.ConsignmentNumber, c.ReceiveDate.ToString("yyyy-MM-dd"), c.Status, c.TotalValue, c.ItemCount, c.OutletName);
            }
            AppendEmptyRow(sdCons);
            AppendTextRow(sdCons, "Return Konsinyasi");
            AppendTextRow(sdCons, "No. Return", "Tanggal", "Status", "Total Qty", "Jml Item", "Outlet");
            foreach (var cr in report.ConsignmentReturns)
            {
                AppendTextRow(sdCons, cr.ReturnNumber, cr.ReturnDate.ToString("yyyy-MM-dd"), cr.Status, cr.TotalQty, cr.ItemCount, cr.OutletName);
            }
            AppendEmptyRow(sdCons);
            AppendTextRow(sdCons, "Penjualan Konsinyasi");
            AppendTextRow(sdCons, "No. Transaksi", "Tanggal", "Produk", "Qty", "Unit Cost", "Total", "Status", "Outlet");
            foreach (var cs in report.ConsignmentSales)
            {
                AppendTextRow(sdCons, cs.TransactionNumber, cs.CreatedAt.ToString("yyyy-MM-dd"), cs.ProductName, cs.Qty, cs.UnitCost, cs.TotalAmount, cs.Status, cs.OutletName);
            }
            AppendEmptyRow(sdCons);
            AppendTextRow(sdCons, "Settlement");
            AppendTextRow(sdCons, "No. Settlement", "Tanggal", "Total", "Status", "Jml Sales", "Outlet");
            foreach (var s in report.Settlements)
            {
                AppendTextRow(sdCons, s.SettlementNumber, s.SettlementDate.ToString("yyyy-MM-dd"), s.TotalAmount, s.Status, s.SalesCount, s.OutletName);
            }
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(wsCons), SheetId = 5, Name = "Konsinyasi_Settlement" });

            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Laporan_Supplier_{report.Supplier?.SupplierName ?? "Supplier"}_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.xlsx");
    }

    public async Task<StockCardReportDto> GetStockCardReportAsync(
        StockCardReportFilters filters,
        CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var (startUtc, endUtc) = NormalizePeriod(filters.DateFrom, filters.DateTo);
        var outletId = await ResolveAccessibleOutletIdAsync(filters.OutletId, ct);

        if (!filters.ProductId.HasValue)
        {
            throw new InvalidOperationException("Produk wajib dipilih.");
        }
        if (!outletId.HasValue)
        {
            throw new InvalidOperationException("Outlet wajib dipilih.");
        }

        var product = await _dbContext.Products.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == filters.ProductId.Value, ct);
        if (product == null)
        {
            throw new InvalidOperationException("Produk tidak ditemukan.");
        }

        ProductVariant? variant = null;
        if (filters.ProductVariantId.HasValue)
        {
            variant = await _dbContext.ProductVariants.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == filters.ProductVariantId.Value && v.ProductId == product.Id, ct);
            if (variant == null)
            {
                throw new InvalidOperationException("Varian produk tidak ditemukan.");
            }
        }
        else if (product.HasVariants)
        {
            // if product has variants but no variant selected, we still allow product-level aggregation? Spec says filter per satu produk/varian dan satu outlet pada satu waktu agar saldo jelas. So we keep product-level if variant null -> sum across variants? But better to require variant if hasVariants?
            // For now allow product-level without variant filter -> query variant null aggregated? We'll filter ProductVariantId == null only when hasVariants false.
        }

        var outlet = await _dbContext.Outlets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == outletId.Value, ct);
        var outletName = outlet?.Name ?? "Outlet";

        var productInfo = new StockCardReportProductInfoDto(
            product.Id,
            product.Name,
            product.Sku,
            variant?.Id,
            variant?.Sku,
            variant != null ? string.Join(", ", variant.AttributeValues.Select(av => av.Value)) : null,
            product.HasVariants);

        var baseQuery = _dbContext.StockLedgers
            .AsNoTracking()
            .Include(sl => sl.Product)
            .Include(sl => sl.ProductVariant)
            .Include(sl => sl.Outlet)
            .Where(sl => sl.OutletId == outletId.Value && sl.ProductId == product.Id);

        if (filters.ProductVariantId.HasValue)
        {
            baseQuery = baseQuery.Where(sl => sl.ProductVariantId == filters.ProductVariantId.Value);
        }
        else
        {
            // If product has no variants, filter where variant null; if has variants and no variant selected, include all variants of this product
            if (!product.HasVariants)
            {
                baseQuery = baseQuery.Where(sl => sl.ProductVariantId == null);
            }
        }

        var openingBalance = await baseQuery
            .Where(sl => sl.CreatedAt < startUtc)
            .SumAsync(sl => sl.QtyChange, ct);

        var mutations = await baseQuery
            .Where(sl => sl.CreatedAt >= startUtc && sl.CreatedAt <= endUtc)
            .OrderBy(sl => sl.CreatedAt)
            .ThenBy(sl => sl.Id)
            .ToListAsync(ct);

        decimal runningBalance = openingBalance;
        var lines = new List<StockCardReportLineDto>();
        decimal totalIn = 0, totalOut = 0;
        foreach (var sl in mutations)
        {
            var qtyIn = sl.QtyChange > 0 ? sl.QtyChange : 0;
            var qtyOut = sl.QtyChange < 0 ? Math.Abs(sl.QtyChange) : 0;
            totalIn += qtyIn;
            totalOut += qtyOut;
            runningBalance += sl.QtyChange;
            var label = MapMovementTypeToLabel(sl.MovementType);
            // Try to resolve reference number for better UX
            string? referenceNumber = null;
            if (sl.ReferenceType == "transaction")
            {
                var trx = await _dbContext.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == sl.ReferenceId, ct);
                referenceNumber = trx?.TransactionNumber;
            }
            else if (sl.ReferenceType == "consignment")
            {
                var c = await _dbContext.Consignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sl.ReferenceId, ct);
                referenceNumber = c?.ConsignmentNumber;
            }
            else if (sl.ReferenceType == "consignment_return")
            {
                var cr = await _dbContext.ConsignmentReturns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sl.ReferenceId, ct);
                referenceNumber = cr?.ReturnNumber;
            }
            else if (sl.ReferenceType == "purchase_order")
            {
                var po = await _dbContext.PurchaseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sl.ReferenceId, ct);
                referenceNumber = po?.PoNumber;
            }

            lines.Add(new StockCardReportLineDto(
                sl.Id,
                sl.CreatedAt,
                sl.ProductId,
                sl.Product?.Name ?? product.Name,
                sl.Product?.Sku ?? product.Sku,
                sl.ProductVariantId,
                sl.ProductVariant?.Sku ?? variant?.Sku,
                sl.MovementType,
                label,
                sl.ReferenceType,
                sl.ReferenceId,
                referenceNumber ?? sl.ReferenceType,
                sl.Note,
                sl.QtyChange,
                qtyIn,
                qtyOut,
                runningBalance,
                sl.Outlet?.Name ?? outletName));
        }

        var summary = new StockCardReportSummaryDto(
            openingBalance,
            totalIn,
            totalOut,
            runningBalance);

        var effectiveFilters = new StockCardReportFilters(filters.DateFrom, filters.DateTo, outletId, filters.ProductId, filters.ProductVariantId);

        return new StockCardReportDto(
            effectiveFilters,
            productInfo,
            outletName,
            summary,
            lines);
    }

    public async Task<ExportReportResponse> ExportStockCardExcelAsync(
        StockCardReportFilters filters,
        CancellationToken ct = default)
    {
        var report = await GetStockCardReportAsync(filters, ct);
        var periodStart = report.Filters.DateFrom?.Date ?? DateTime.UtcNow.Date;
        var periodEnd = report.Filters.DateTo?.Date ?? DateTime.UtcNow.Date;
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            AppendTextRow(sheetData, "Kartu Stok MorrusPOS");
            AppendTextRow(sheetData, $"Produk: {report.Product.ProductName} ({report.Product.Sku}){(report.Product.ProductVariantId.HasValue ? $" - Varian: {report.Product.VariantSku}" : string.Empty)}");
            AppendTextRow(sheetData, $"Outlet: {report.OutletName}");
            AppendTextRow(sheetData, $"Periode: {periodStart:yyyy-MM-dd} s/d {periodEnd:yyyy-MM-dd}");
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "Ringkasan");
            AppendTextRow(sheetData, "Metrik", "Nilai");
            AppendTextRow(sheetData, "Saldo Awal", report.Summary.OpeningBalance);
            AppendTextRow(sheetData, "Total Masuk", report.Summary.TotalIn);
            AppendTextRow(sheetData, "Total Keluar", report.Summary.TotalOut);
            AppendTextRow(sheetData, "Saldo Akhir", report.Summary.ClosingBalance);
            AppendEmptyRow(sheetData);

            AppendTextRow(sheetData, "Mutasi");
            AppendTextRow(sheetData, "Tanggal/Waktu", "Produk-Varian", "Jenis Mutasi", "Referensi", "Catatan", "Masuk", "Keluar", "Saldo Berjalan");
            foreach (var line in report.Lines)
            {
                var productVariantLabel = line.ProductVariantId.HasValue ? $"{line.ProductName} - {line.VariantSku}" : line.ProductName;
                AppendTextRow(sheetData,
                    line.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    productVariantLabel,
                    line.MovementLabel,
                    line.ReferenceNumber ?? line.ReferenceType,
                    line.Note ?? string.Empty,
                    line.QtyIn,
                    line.QtyOut,
                    line.RunningBalance);
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Kartu Stok" });
            workbookPart.Workbook.Save();
        }

        return new ExportReportResponse(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Kartu_Stok_{report.Product.Sku}_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.xlsx");
    }

    private static string MapMovementTypeToLabel(string movementType)
    {
        return movementType switch
        {
            StockMovementType.Sale => "Penjualan",
            StockMovementType.Return => "Retur Penjualan",
            StockMovementType.PurchaseIn => "Pembelian",
            StockMovementType.TransferIn => "Transfer Masuk",
            StockMovementType.TransferOut => "Transfer Keluar",
            StockMovementType.OpnameAdjustment => "Penyesuaian Opname",
            StockMovementType.ConsignmentIn => "Ambil Konsinyasi",
            StockMovementType.ConsignmentReturn => "Return Konsinyasi",
            _ => movementType
        };
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
