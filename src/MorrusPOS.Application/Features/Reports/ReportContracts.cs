using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MorrusPOS.Application.Features.Reports;

public record ProfitLossReportDto(
    DateTime StartDate,
    DateTime EndDate,
    Guid? OutletId,
    string OutletName,
    decimal GrossRevenue,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal NetRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    IReadOnlyList<ProfitLossCategorySummaryDto> CategoryBreakdown
);

public record ProfitLossCategorySummaryDto(
    Guid CategoryId,
    string CategoryName,
    decimal Revenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit
);

public record ExportReportResponse(
    byte[] FileBytes,
    string ContentType,
    string FileName
);

public interface IReportService
{
    Task<ProfitLossReportDto> GetProfitLossReportAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);

    Task<ExportReportResponse> ExportProfitLossExcelAsync(
        Guid? outletId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default);
}
