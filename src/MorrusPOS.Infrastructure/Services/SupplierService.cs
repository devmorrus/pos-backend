using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _dbContext;

    public SupplierService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SupplierDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { id }, ct);
        if (supplier == null)
            throw new InvalidOperationException("Supplier tidak ditemukan.");

        return MapToDto(supplier);
    }

    public async Task<IReadOnlyList<SupplierDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var suppliers = await _dbContext.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return suppliers.Select(MapToDto).ToList();
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        var trimmedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new InvalidOperationException("Nama supplier wajib diisi.");

        var nameLower = trimmedName.ToLower();
        var exists = await _dbContext.Suppliers.AnyAsync(s => s.Name.ToLower() == nameLower, ct);
        if (exists)
            throw new InvalidOperationException("Supplier dengan nama yang sama sudah terdaftar.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            ContactPerson = request.ContactPerson?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(ct);

        return MapToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await _dbContext.Suppliers.FindAsync(new object[] { id }, ct);
        if (supplier == null)
            throw new InvalidOperationException("Supplier tidak ditemukan.");

        var trimmedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new InvalidOperationException("Nama supplier wajib diisi.");

        var nameLower = trimmedName.ToLower();
        var exists = await _dbContext.Suppliers.AnyAsync(s => s.Id != id && s.Name.ToLower() == nameLower, ct);
        if (exists)
            throw new InvalidOperationException("Supplier dengan nama yang sama sudah terdaftar.");

        supplier.Name = trimmedName;
        supplier.ContactPerson = request.ContactPerson?.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(supplier);
    }

    private static SupplierDto MapToDto(Supplier s) => new(
        s.Id, s.Name, s.ContactPerson, s.Phone, s.Email, s.Address, s.IsActive
    );
}
