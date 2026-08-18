using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Customers;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CustomerService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(CustomerListQuery query, CancellationToken ct = default)
    {
        EnsureCustomerViewAccess();

        var take = Math.Clamp(query.Take, 1, 100);
        var customersQuery = _dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var normalized = NormalizePhone(query.Q);
            var search = query.Q.Trim().ToLowerInvariant();
            customersQuery = customersQuery.Where(customer =>
                customer.Phone.Contains(normalized) ||
                customer.Name.ToLower().Contains(search) ||
                customer.CustomerCode.ToLower().Contains(search));
        }

        if (query.IsMember.HasValue)
        {
            customersQuery = customersQuery.Where(customer => customer.IsMember == query.IsMember.Value);
        }

        if (query.IsActive.HasValue)
        {
            customersQuery = customersQuery.Where(customer => customer.IsActive == query.IsActive.Value);
        }

        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value.Date;
            customersQuery = customersQuery.Where(customer => customer.CreatedAt >= from);
        }

        if (query.DateTo.HasValue)
        {
            var toExclusive = query.DateTo.Value.Date.AddDays(1);
            customersQuery = customersQuery.Where(customer => customer.CreatedAt < toExclusive);
        }

        var customers = await customersQuery
            .OrderByDescending(customer => customer.LastTransactionAt ?? customer.CreatedAt)
            .ThenBy(customer => customer.Name)
            .Take(take)
            .ToListAsync(ct);

        return customers.Select(MapToListItemDto).ToList();
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        EnsureCustomerViewAccess();
        var customer = await FindCustomerAsync(id, ct);
        return MapToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        EnsureCustomerManageAccess();
        var businessId = _currentUserService.BusinessId ?? throw new UnauthorizedAccessException("Business user tidak valid.");
        var normalizedPhone = NormalizePhone(request.Phone);
        await EnsurePhoneUniqueAsync(normalizedPhone, null, ct);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CreatedOutletId = _currentUserService.OutletId,
            CustomerCode = await GenerateCustomerCodeAsync(ct),
            Name = request.Name.Trim(),
            Phone = normalizedPhone,
            Email = NormalizeOptional(request.Email),
            Gender = NormalizeOptional(request.Gender),
            BirthDate = request.BirthDate?.Date,
            Notes = NormalizeOptional(request.Notes),
            IsActive = request.IsActive,
            IsMember = true,
            JoinedAt = DateTime.UtcNow,
            MemberStatus = request.IsActive ? CustomerMemberStatus.Active : CustomerMemberStatus.Inactive,
            CreditLimit = request.CreditLimit,
            CurrentDebt = 0,
            KtpNumber = NormalizeOptional(request.KtpNumber),
            Address = NormalizeOptional(request.Address),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        EnsureCustomerManageAccess();
        var customer = await FindCustomerAsync(id, ct);
        var normalizedPhone = NormalizePhone(request.Phone);
        await EnsurePhoneUniqueAsync(normalizedPhone, customer.Id, ct);

        customer.Name = request.Name.Trim();
        customer.Phone = normalizedPhone;
        customer.Email = NormalizeOptional(request.Email);
        customer.Gender = NormalizeOptional(request.Gender);
        customer.BirthDate = request.BirthDate?.Date;
        customer.Notes = NormalizeOptional(request.Notes);
        customer.IsActive = request.IsActive;
        customer.MemberStatus = request.IsActive ? CustomerMemberStatus.Active : CustomerMemberStatus.Inactive;
        customer.CreditLimit = request.CreditLimit;
        customer.KtpNumber = NormalizeOptional(request.KtpNumber);
        customer.Address = NormalizeOptional(request.Address);
        customer.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(customer);
    }

    public async Task<IReadOnlyList<CustomerListItemDto>> LookupAsync(string query, int take = 10, CancellationToken ct = default)
    {
        EnsureCustomerViewAccess();

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalized = NormalizePhone(query);
        var lowered = query.Trim().ToLowerInvariant();
        var safeTake = Math.Clamp(take, 1, 20);

        var customers = await _dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.IsActive)
            .Where(customer =>
                customer.Phone.Contains(normalized) ||
                customer.Name.ToLower().Contains(lowered) ||
                customer.CustomerCode.ToLower().Contains(lowered))
            .OrderBy(customer => customer.Phone.StartsWith(normalized) ? 0 : 1)
            .ThenByDescending(customer => customer.LastTransactionAt ?? customer.CreatedAt)
            .Take(safeTake)
            .ToListAsync(ct);

        return customers.Select(MapToListItemDto).ToList();
    }

    public async Task<IReadOnlyList<TransactionListItemDto>> GetTransactionsAsync(Guid id, int take = 20, CancellationToken ct = default)
    {
        EnsureCustomerViewAccess();
        _ = await FindCustomerAsync(id, ct);

        var transactions = await _dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Outlet)
            .Include(transaction => transaction.User)
            .Include(transaction => transaction.Payments)
            .Where(transaction => transaction.CustomerId == id)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

        return transactions.Select(MapTransactionListItem).ToList();
    }

    private async Task<Customer> FindCustomerAsync(Guid id, CancellationToken ct)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (customer == null)
        {
            throw new InvalidOperationException("Customer tidak ditemukan.");
        }

        return customer;
    }

    private async Task EnsurePhoneUniqueAsync(string normalizedPhone, Guid? currentCustomerId, CancellationToken ct)
    {
        var exists = await _dbContext.Customers.AnyAsync(customer =>
            customer.Phone == normalizedPhone &&
            customer.IsActive &&
            customer.Id != currentCustomerId, ct);

        if (exists)
        {
            throw new InvalidOperationException("Nomor HP sudah dipakai customer aktif lain di business ini.");
        }
    }

    private async Task<string> GenerateCustomerCodeAsync(CancellationToken ct)
    {
        var prefix = $"CUS-{DateTime.UtcNow:yyyyMMdd}";
        var countToday = await _dbContext.Customers.CountAsync(customer => customer.CustomerCode.StartsWith(prefix), ct);
        return $"{prefix}-{countToday + 1:D4}";
    }

    private void EnsureCustomerManageAccess()
    {
        if (_currentUserService.Role is "Owner" or "Admin" or "Keuangan" or "KepalaCabang")
        {
            return;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses mengelola customer.");
    }

    private void EnsureCustomerViewAccess()
    {
        if (_currentUserService.Role is "Owner" or "Admin" or "Keuangan" or "KepalaCabang" or "Kasir")
        {
            return;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses customer.");
    }

    private static CustomerListItemDto MapToListItemDto(Customer customer) =>
        new(
            customer.Id,
            customer.CustomerCode,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.IsMember,
            customer.MemberStatus,
            customer.IsActive,
            customer.LifetimeSpend,
            customer.LastTransactionAt,
            customer.CreatedAt,
            customer.CreditLimit,
            customer.CurrentDebt,
            customer.KtpNumber,
            customer.Address
        );

    private static CustomerDto MapToDto(Customer customer) =>
        new(
            customer.Id,
            customer.BusinessId,
            customer.CreatedOutletId,
            customer.CustomerCode,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.Gender,
            customer.BirthDate,
            customer.Notes,
            customer.IsActive,
            customer.IsMember,
            customer.MemberStatus,
            customer.PointsBalance,
            customer.LifetimeSpend,
            customer.JoinedAt,
            customer.LastTransactionAt,
            customer.CreatedAt,
            customer.UpdatedAt,
            customer.CreditLimit,
            customer.CurrentDebt,
            customer.KtpNumber,
            customer.Address
        );

    private static TransactionListItemDto MapTransactionListItem(Transaction transaction)
    {
        var firstPayment = transaction.Payments.FirstOrDefault();
        var paymentSummary = transaction.Payments.Count switch
        {
            0 => "-",
            1 => firstPayment?.Method ?? "-",
            _ => $"{firstPayment?.Method ?? "-"} +{transaction.Payments.Count - 1}",
        };

        return new TransactionListItemDto(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.OutletId,
            transaction.Outlet?.Name ?? string.Empty,
            transaction.UserId,
            transaction.User?.Name ?? string.Empty,
            transaction.GrandTotal,
            transaction.Status,
            transaction.Channel,
            transaction.CustomerId,
            transaction.CustomerNameSnapshot,
            transaction.CustomerPhoneSnapshot,
            transaction.CustomerType,
            transaction.ExternalCustomerReference,
            transaction.CreatedAt,
            paymentSummary
        );
    }

    public static string NormalizePhone(string value)
    {
        var trimmed = value.Trim();
        var chars = trimmed.Where(ch => char.IsDigit(ch) || ch == '+').ToArray();
        return new string(chars);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
