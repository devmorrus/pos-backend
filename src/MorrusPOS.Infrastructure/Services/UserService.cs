using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Users;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public UserService(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Outlet)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user == null)
        {
            throw new InvalidOperationException("Pengguna tidak ditemukan.");
        }

        // Tenant check: Non-owner cannot view users from other outlets
        if (_currentUserService.Role != "Owner" && user.OutletId != _currentUserService.OutletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke pengguna ini.");
        }

        return MapToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetByOutletAsync(Guid? outletId, CancellationToken ct = default)
    {
        // Tenant restriction
        if (_currentUserService.Role != "Owner")
        {
            outletId = _currentUserService.OutletId;
        }

        var query = _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Outlet)
            .AsNoTracking();

        if (outletId.HasValue)
        {
            query = query.Where(u => u.OutletId == outletId.Value);
        }
        else if (_currentUserService.Role != "Owner")
        {
            // Non-owners must be filtered by their outlet, shouldn't access all
            query = query.Where(u => u.OutletId == _currentUserService.OutletId);
        }

        var users = await query.ToListAsync(ct);
        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        // 1. Email uniqueness check
        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);
        if (emailExists)
        {
            throw new InvalidOperationException("Email sudah terdaftar.");
        }

        // 2. Validate Role
        var role = await _dbContext.Roles.FindAsync(new object[] { request.RoleId }, ct);
        if (role == null)
        {
            throw new InvalidOperationException("Role tidak valid.");
        }

        // 3. Security/Tenant Checks
        var currentUserRole = _currentUserService.Role;
        var currentUserOutlet = _currentUserService.OutletId;

        if (currentUserRole != "Owner")
        {
            // Non-owners can only create users for their own outlet
            if (request.OutletId != currentUserOutlet)
            {
                throw new UnauthorizedAccessException("Anda hanya dapat membuat pengguna untuk outlet Anda sendiri.");
            }

            // Non-owners cannot assign Owner role
            if (role.Name == "Owner")
            {
                throw new UnauthorizedAccessException("Hanya Owner yang dapat menetapkan role Owner.");
            }
        }

        // 4. Validate Outlet if provided
        if (request.OutletId.HasValue)
        {
            var outlet = await _dbContext.Outlets
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.OutletId.Value, ct);

            if (outlet == null)
            {
                throw new InvalidOperationException("Outlet tidak valid.");
            }

            if (!outlet.IsActive)
            {
                throw new InvalidOperationException("Outlet nonaktif tidak dapat dipakai untuk pengguna baru.");
            }
        }
        else
        {
            // If no outlet is provided, it must be Owner role assigning
            if (role.Name != "Owner")
            {
                throw new InvalidOperationException("Selain Owner, pengguna wajib diasosiasikan dengan Outlet.");
            }
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            OutletId = request.OutletId,
            RoleId = request.RoleId,
            Name = request.Name,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(ct);

        // Fetch user with joined properties for DTO mapping
        return await GetByIdAsync(newUser.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            throw new InvalidOperationException("Pengguna tidak ditemukan.");
        }

        // Tenant Check
        var currentUserRole = _currentUserService.Role;
        var currentUserOutlet = _currentUserService.OutletId;

        if (currentUserRole != "Owner")
        {
            if (user.OutletId != currentUserOutlet || request.OutletId != currentUserOutlet)
            {
                throw new UnauthorizedAccessException("Anda tidak memiliki akses untuk mengupdate pengguna di outlet lain.");
            }
        }

        // Email uniqueness check if changed
        if (user.Email.ToLower() != request.Email.ToLower())
        {
            var emailExists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower() && u.Id != id, ct);
            if (emailExists)
            {
                throw new InvalidOperationException("Email sudah terdaftar pada pengguna lain.");
            }
        }

        // Validate Role
        var role = await _dbContext.Roles.FindAsync(new object[] { request.RoleId }, ct);
        if (role == null)
        {
            throw new InvalidOperationException("Role tidak valid.");
        }

        if (currentUserRole != "Owner" && role.Name == "Owner")
        {
            throw new UnauthorizedAccessException("Hanya Owner yang dapat menetapkan role Owner.");
        }

        // Validate Outlet
        if (request.OutletId.HasValue)
        {
            var outlet = await _dbContext.Outlets
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.OutletId.Value, ct);

            if (outlet == null)
            {
                throw new InvalidOperationException("Outlet tidak valid.");
            }

            if (!outlet.IsActive)
            {
                throw new InvalidOperationException("Outlet nonaktif tidak dapat dipakai untuk assignment pengguna.");
            }
        }
        else
        {
            if (role.Name != "Owner")
            {
                throw new InvalidOperationException("Selain Owner, pengguna wajib diasosiasikan dengan Outlet.");
            }
        }

        user.Name = request.Name;
        user.Email = request.Email;
        user.RoleId = request.RoleId;
        user.OutletId = request.OutletId;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            throw new InvalidOperationException("Pengguna tidak ditemukan.");
        }

        // Tenant Check
        if (_currentUserService.Role != "Owner" && user.OutletId != _currentUserService.OutletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke pengguna ini.");
        }

        // Prevent self deactivation/deletion
        if (_currentUserService.UserId == id)
        {
            throw new InvalidOperationException("Anda tidak dapat menghapus akun Anda sendiri.");
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            throw new InvalidOperationException("Pengguna tidak ditemukan.");
        }

        // Security check: Users can only change their own password, unless Owner
        if (_currentUserService.Role != "Owner" && _currentUserService.UserId != id)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki wewenang mengubah password pengguna ini.");
        }

        // Verify current password
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("Password lama salah.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto(
            user.Id,
            user.OutletId,
            user.Outlet?.Name,
            user.RoleId,
            user.Role.Name,
            user.Name,
            user.Email,
            user.IsActive,
            user.LastLoginAt,
            user.CreatedAt,
            user.UpdatedAt
        );
    }
}
