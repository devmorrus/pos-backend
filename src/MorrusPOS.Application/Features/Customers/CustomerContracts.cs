using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.Application.Features.Customers;

public record CustomerListItemDto(
    Guid Id,
    string CustomerCode,
    string Name,
    string Phone,
    string? Email,
    bool IsMember,
    string MemberStatus,
    bool IsActive,
    decimal LifetimeSpend,
    DateTime? LastTransactionAt,
    DateTime CreatedAt,
    decimal CreditLimit,
    decimal CurrentDebt,
    string? KtpNumber,
    string? Address
);

public record CustomerDto(
    Guid Id,
    Guid? BusinessId,
    Guid? CreatedOutletId,
    string CustomerCode,
    string Name,
    string Phone,
    string? Email,
    string? Gender,
    DateTime? BirthDate,
    string? Notes,
    bool IsActive,
    bool IsMember,
    string MemberStatus,
    decimal PointsBalance,
    decimal LifetimeSpend,
    DateTime? JoinedAt,
    DateTime? LastTransactionAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal CreditLimit,
    decimal CurrentDebt,
    string? KtpNumber,
    string? Address
);

public record CreateCustomerRequest(
    string Name,
    string Phone,
    string? Email,
    string? Gender,
    DateTime? BirthDate,
    string? Notes,
    bool IsActive = true,
    decimal CreditLimit = 0,
    string? KtpNumber = null,
    string? Address = null
);

public record UpdateCustomerRequest(
    string Name,
    string Phone,
    string? Email,
    string? Gender,
    DateTime? BirthDate,
    string? Notes,
    bool IsActive,
    decimal CreditLimit,
    string? KtpNumber,
    string? Address
);

public record CustomerListQuery(
    string? Q,
    bool? IsMember,
    bool? IsActive,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Take = 20
);

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(CustomerListQuery query, CancellationToken ct = default);
    Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerListItemDto>> LookupAsync(string query, int take = 10, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionListItemDto>> GetTransactionsAsync(Guid id, int take = 20, CancellationToken ct = default);
}
