using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ChartOfAccountService : IChartOfAccountService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ChartOfAccountService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<ChartOfAccountDto>> GetAllAsync(CancellationToken ct = default)
    {
        EnsureBusinessContext();

        var accounts = await _dbContext.ChartOfAccounts
            .Include(account => account.Outlet)
            .Include(account => account.ParentAccount)
            .AsNoTracking()
            .OrderBy(account => account.AccountCode)
            .ThenBy(account => account.AccountName)
            .ToListAsync(ct);

        return accounts.Select(MapToDto).ToList();
    }

    public async Task<ChartOfAccountDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        EnsureBusinessContext();

        var account = await LoadAccountAsync(id, ct);
        if (account == null)
        {
            throw new InvalidOperationException("Akun tidak ditemukan.");
        }

        return MapToDto(account);
    }

    public async Task<ChartOfAccountDto> CreateAsync(CreateChartOfAccountRequest request, CancellationToken ct = default)
    {
        var normalizedCode = NormalizeCode(request.AccountCode);
        var normalizedName = NormalizeName(request.AccountName);
        var normalizedType = NormalizeType(request.AccountType);
        var normalizedCashBank = request.IsCashBank;
        var businessId = EnsureBusinessContext();

        await ValidateMutationAsync(
            currentAccountId: null,
            normalizedCode,
            normalizedType,
            normalizedCashBank,
            request.OutletId,
            request.ParentAccountId,
            ct);

        var entity = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            OutletId = request.OutletId,
            ParentAccountId = request.ParentAccountId,
            AccountCode = normalizedCode,
            AccountName = normalizedName,
            AccountType = normalizedType,
            IsCashBank = normalizedCashBank,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ChartOfAccounts.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        var saved = await LoadAccountAsync(entity.Id, ct);
        return MapToDto(saved!);
    }

    public async Task<ChartOfAccountDto> UpdateAsync(Guid id, UpdateChartOfAccountRequest request, CancellationToken ct = default)
    {
        var account = await _dbContext.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null)
        {
            throw new InvalidOperationException("Akun tidak ditemukan.");
        }

        var normalizedCode = NormalizeCode(request.AccountCode);
        var normalizedName = NormalizeName(request.AccountName);
        var normalizedType = NormalizeType(request.AccountType);
        var normalizedCashBank = request.IsCashBank;

        await ValidateMutationAsync(
            currentAccountId: id,
            normalizedCode,
            normalizedType,
            normalizedCashBank,
            request.OutletId,
            request.ParentAccountId,
            ct);

        account.AccountCode = normalizedCode;
        account.AccountName = normalizedName;
        account.AccountType = normalizedType;
        account.IsCashBank = normalizedCashBank;
        account.OutletId = request.OutletId;
        account.ParentAccountId = request.ParentAccountId;
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var saved = await LoadAccountAsync(account.Id, ct);
        return MapToDto(saved!);
    }

    public async Task<ChartOfAccountDto> UpdateStatusAsync(Guid id, UpdateChartOfAccountStatusRequest request, CancellationToken ct = default)
    {
        var account = await _dbContext.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null)
        {
            throw new InvalidOperationException("Akun tidak ditemukan.");
        }

        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var saved = await LoadAccountAsync(account.Id, ct);
        return MapToDto(saved!);
    }

    private async Task ValidateMutationAsync(
        Guid? currentAccountId,
        string normalizedCode,
        string normalizedType,
        bool normalizedCashBank,
        Guid? outletId,
        Guid? parentAccountId,
        CancellationToken ct)
    {
        if (normalizedCashBank && normalizedType != ChartOfAccountType.Asset)
        {
            throw new InvalidOperationException("Akun kas/bank hanya boleh menggunakan tipe asset.");
        }

        var duplicateCodeExists = await _dbContext.ChartOfAccounts
            .AnyAsync(
                account => account.AccountCode == normalizedCode
                    && (!currentAccountId.HasValue || account.Id != currentAccountId.Value),
                ct);
        if (duplicateCodeExists)
        {
            throw new InvalidOperationException("Kode akun sudah digunakan.");
        }

        Outlet? outlet = null;
        if (outletId.HasValue)
        {
            outlet = await EnsureOutletAccessibleAsync(outletId.Value, ct);
        }

        if (parentAccountId.HasValue)
        {
            if (currentAccountId.HasValue && parentAccountId.Value == currentAccountId.Value)
            {
                throw new InvalidOperationException("Akun induk tidak boleh menunjuk dirinya sendiri.");
            }

            var parent = await _dbContext.ChartOfAccounts
                .Include(account => account.Outlet)
                .FirstOrDefaultAsync(account => account.Id == parentAccountId.Value, ct);

            if (parent == null)
            {
                throw new InvalidOperationException("Akun induk tidak ditemukan.");
            }

            if (parent.AccountType != normalizedType)
            {
                throw new InvalidOperationException("Akun induk harus memiliki tipe akun yang sama.");
            }

            if (outletId.HasValue)
            {
                if (parent.OutletId.HasValue && parent.OutletId != outletId)
                {
                    throw new InvalidOperationException("Akun induk outlet harus berasal dari outlet yang sama.");
                }
            }
            else if (parent.OutletId.HasValue)
            {
                throw new InvalidOperationException("Akun global business tidak boleh memakai akun induk khusus outlet.");
            }

            if (currentAccountId.HasValue)
            {
                var createsCycle = await CreatesCircularReferenceAsync(currentAccountId.Value, parent.Id, ct);
                if (createsCycle)
                {
                    throw new InvalidOperationException("Relasi akun induk membentuk siklus yang tidak valid.");
                }
            }
        }

        if (outlet != null && !_CanAccessOutlet(outlet.Id))
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }
    }

    private async Task<bool> CreatesCircularReferenceAsync(Guid currentAccountId, Guid parentAccountId, CancellationToken ct)
    {
        var cursorId = parentAccountId;
        while (true)
        {
            if (cursorId == currentAccountId)
            {
                return true;
            }

            var cursor = await _dbContext.ChartOfAccounts
                .AsNoTracking()
                .Where(account => account.Id == cursorId)
                .Select(account => new { account.ParentAccountId })
                .FirstOrDefaultAsync(ct);

            if (cursor?.ParentAccountId is not Guid nextId)
            {
                return false;
            }

            cursorId = nextId;
        }
    }

    private async Task<ChartOfAccount?> LoadAccountAsync(Guid id, CancellationToken ct)
    {
        return await _dbContext.ChartOfAccounts
            .Include(account => account.Outlet)
            .Include(account => account.ParentAccount)
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == id, ct);
    }

    private async Task<Outlet> EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == outletId, ct);

        if (outlet == null)
        {
            throw new InvalidOperationException("Outlet tidak ditemukan.");
        }

        if (!_CanAccessOutlet(outletId))
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }

        return outlet;
    }

    private bool _CanAccessOutlet(Guid outletId)
    {
        return _currentUserService.Role is "Owner" or "Admin" or "Keuangan"
            || _currentUserService.OutletId == outletId;
    }

    private Guid EnsureBusinessContext()
    {
        if (!_currentUserService.BusinessId.HasValue)
        {
            throw new UnauthorizedAccessException("Business context tidak ditemukan.");
        }

        return _currentUserService.BusinessId.Value;
    }

    private static string NormalizeCode(string accountCode)
    {
        return accountCode.Trim().ToUpperInvariant();
    }

    private static string NormalizeName(string accountName)
    {
        return accountName.Trim();
    }

    private static string NormalizeType(string accountType)
    {
        return accountType.Trim().ToLowerInvariant();
    }

    private static ChartOfAccountDto MapToDto(ChartOfAccount account)
    {
        return new ChartOfAccountDto(
            account.Id,
            account.BusinessId,
            account.AccountCode,
            account.AccountName,
            account.AccountType,
            account.IsCashBank,
            account.IsActive,
            account.OutletId,
            account.Outlet?.Name,
            account.ParentAccountId,
            account.ParentAccount?.AccountName,
            account.CreatedAt,
            account.UpdatedAt
        );
    }
}
