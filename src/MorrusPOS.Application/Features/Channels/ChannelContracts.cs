namespace MorrusPOS.Application.Features.Channels;

public record ChannelAccountDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    string Name,
    string ChannelName,
    string MerchantId,
    decimal DefaultCommissionRate,
    bool IsActive
);

public record CreateChannelAccountRequest(
    Guid OutletId,
    string Name,
    string ChannelName,
    string? MerchantId,
    decimal DefaultCommissionRate,
    bool IsActive
);

public record UpdateChannelAccountRequest(
    Guid OutletId,
    string Name,
    string ChannelName,
    string? MerchantId,
    decimal DefaultCommissionRate,
    bool IsActive
);

public record ChannelSettlementEligibleTransactionDto(
    Guid TransactionId,
    string TransactionNumber,
    Guid OutletId,
    string OutletName,
    DateTime CreatedAt,
    decimal GrandTotal,
    string Channel,
    string CashierName
);

public record ChannelSettlementItemDto(
    Guid TransactionId,
    string TransactionNumber,
    DateTime TransactionDate,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount
);

public record ChannelSettlementListItemDto(
    Guid Id,
    string SettlementNumber,
    Guid ChannelAccountId,
    string ChannelAccountName,
    Guid OutletId,
    string OutletName,
    DateTime SettlementDate,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount,
    string Status
);

public record ChannelSettlementDto(
    Guid Id,
    string SettlementNumber,
    Guid ChannelAccountId,
    string ChannelAccountName,
    string ChannelName,
    Guid OutletId,
    string OutletName,
    DateTime SettlementDate,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount,
    decimal CommissionRate,
    string Status,
    Guid CreatedBy,
    string CreatedByName,
    IReadOnlyList<ChannelSettlementItemDto> Items
);

public record CreateChannelSettlementRequest(
    Guid ChannelAccountId,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    decimal? CommissionAmountOverride,
    List<Guid> TransactionIds
);

public record UpdateChannelSettlementRequest(
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    decimal? CommissionAmountOverride,
    List<Guid> TransactionIds
);

public record UpdateChannelSettlementStatusRequest(
    string Status
);

public record ChannelSettlementFilters(
    Guid? OutletId,
    Guid? ChannelAccountId,
    string? Status,
    DateTime? DateFrom,
    DateTime? DateTo
);

public interface IChannelAccountService
{
    Task<IReadOnlyList<ChannelAccountDto>> GetAsync(Guid? outletId, CancellationToken ct = default);
    Task<ChannelAccountDto> CreateAsync(CreateChannelAccountRequest request, CancellationToken ct = default);
    Task<ChannelAccountDto> UpdateAsync(Guid id, UpdateChannelAccountRequest request, CancellationToken ct = default);
}

public interface IChannelSettlementService
{
    Task<IReadOnlyList<ChannelSettlementListItemDto>> GetAsync(ChannelSettlementFilters filters, CancellationToken ct = default);
    Task<ChannelSettlementDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ChannelSettlementEligibleTransactionDto>> GetEligibleTransactionsAsync(
        Guid channelAccountId,
        DateTime periodStartDate,
        DateTime periodEndDate,
        Guid? excludeSettlementId,
        CancellationToken ct = default);
    Task<ChannelSettlementDto> CreateAsync(Guid userId, CreateChannelSettlementRequest request, CancellationToken ct = default);
    Task<ChannelSettlementDto> UpdateAsync(Guid userId, Guid id, UpdateChannelSettlementRequest request, CancellationToken ct = default);
    Task<ChannelSettlementDto> UpdateStatusAsync(Guid userId, Guid id, UpdateChannelSettlementStatusRequest request, CancellationToken ct = default);
}
