namespace MorrusPOS.Application.Features.Capabilities;

public record IndustryProfileDto(
    Guid Id,
    string Code,
    string Name,
    string? Description);

public record EffectiveCapabilityDto(
    Guid Id,
    string Code,
    string Name,
    string Module,
    string Scope,
    bool Enabled,
    bool BusinessEnabled,
    bool? ProfileDefault,
    bool? BusinessOverride,
    bool? OutletOverride,
    string Source,
    bool CanOverride);

public record EffectiveCapabilitySnapshot(
    Guid BusinessId,
    Guid? OutletId,
    long Revision,
    IndustryProfileDto IndustryProfile,
    IReadOnlyList<EffectiveCapabilityDto> Capabilities);

public record CapabilityOverrideItem(string Code, bool? Enabled);

public record UpdateCapabilityOverridesRequest(
    long ExpectedRevision,
    string? Reason,
    IReadOnlyList<CapabilityOverrideItem> Items);

public record UpdateIndustryProfileRequest(
    string ProfileCode,
    long ExpectedRevision,
    string? Reason);

public record CapabilityChangeLogDto(
    Guid Id,
    Guid? OutletId,
    string? OutletName,
    string? CapabilityCode,
    string? CapabilityName,
    string ChangeType,
    bool? OldValue,
    bool? NewValue,
    string? Reason,
    Guid ChangedByUserId,
    string ChangedByUserName,
    DateTime CreatedAt);

public interface ICapabilityEvaluator
{
    Task<EffectiveCapabilitySnapshot> GetEffectiveAsync(Guid? outletId, CancellationToken ct = default);
    Task<bool> IsEnabledAsync(string capabilityCode, Guid? outletId, CancellationToken ct = default);
    Task EnsureEnabledAsync(string capabilityCode, Guid? outletId, CancellationToken ct = default);
    void Invalidate();
}

public interface ICapabilityAdminService
{
    Task<IReadOnlyList<IndustryProfileDto>> GetIndustryProfilesAsync(CancellationToken ct = default);
    Task<EffectiveCapabilitySnapshot> UpdateBusinessOverridesAsync(UpdateCapabilityOverridesRequest request, CancellationToken ct = default);
    Task<EffectiveCapabilitySnapshot> UpdateOutletOverridesAsync(Guid outletId, UpdateCapabilityOverridesRequest request, CancellationToken ct = default);
    Task<EffectiveCapabilitySnapshot> UpdateIndustryProfileAsync(UpdateIndustryProfileRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CapabilityChangeLogDto>> GetHistoryAsync(
        Guid? outletId,
        string? capabilityCode,
        DateTime? from,
        DateTime? to,
        int take,
        CancellationToken ct = default);
}

public sealed class CapabilityDisabledException : Exception
{
    public CapabilityDisabledException(string capabilityCode, Guid? outletId)
        : base($"Capability '{capabilityCode}' tidak aktif{(outletId.HasValue ? $" untuk outlet {outletId}" : string.Empty)}.")
    {
        CapabilityCode = capabilityCode;
        OutletId = outletId;
    }

    public string CapabilityCode { get; }
    public Guid? OutletId { get; }
}

public sealed class CapabilityRevisionConflictException : Exception
{
    public CapabilityRevisionConflictException()
        : base("Konfigurasi capability telah berubah. Muat ulang lalu coba kembali.") { }
}

public sealed class CapabilityParentDisabledException : Exception
{
    public CapabilityParentDisabledException(string capabilityCode)
        : base($"Capability '{capabilityCode}' dinonaktifkan pada level Business.") { }
}
