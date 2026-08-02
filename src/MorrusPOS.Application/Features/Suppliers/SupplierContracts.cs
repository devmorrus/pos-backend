namespace MorrusPOS.Application.Features.Suppliers;

public record SupplierDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive
);

public record CreateSupplierRequest(
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address
);

public record UpdateSupplierRequest(
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive
);

public interface ISupplierService
{
    Task<SupplierDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierDto>> GetAllActiveAsync(CancellationToken ct = default);
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct = default);
}
