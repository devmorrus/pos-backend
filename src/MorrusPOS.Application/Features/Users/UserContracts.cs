namespace MorrusPOS.Application.Features.Users;

public record UserDto(
    Guid Id,
    Guid? OutletId,
    string? OutletName,
    Guid RoleId,
    string RoleName,
    string Name,
    string Email,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateUserRequest(
    Guid? OutletId,
    Guid RoleId,
    string Name,
    string Email,
    string Password
);

public record UpdateUserRequest(
    string Name,
    string Email,
    Guid RoleId,
    Guid? OutletId,
    bool IsActive
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public interface IUserService
{
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetByOutletAsync(Guid? outletId, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken ct = default);
}
