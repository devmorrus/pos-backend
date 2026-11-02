using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public sealed class CapabilityEvaluator : ICapabilityEvaluator
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly Dictionary<Guid, EffectiveCapabilitySnapshot> _cache = [];

    public CapabilityEvaluator(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EffectiveCapabilitySnapshot> GetEffectiveAsync(Guid? outletId, CancellationToken ct = default)
    {
        var cacheKey = outletId ?? Guid.Empty;
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var businessId = _currentUser.BusinessId
            ?? throw new InvalidOperationException("Business context wajib tersedia untuk evaluasi capability.");

        if (_currentUser.OutletId.HasValue)
        {
            if (outletId.HasValue && outletId != _currentUser.OutletId)
                throw new UnauthorizedAccessException("Outlet tidak dapat diakses.");
            outletId = _currentUser.OutletId;
            cacheKey = outletId.Value;
        }

        if (outletId.HasValue && !await _db.Outlets.AsNoTracking().AnyAsync(x => x.Id == outletId, ct))
            throw new KeyNotFoundException("Outlet tidak ditemukan.");

        var business = await _db.Businesses
            .AsNoTracking()
            .Include(x => x.IndustryProfile)
            .SingleAsync(x => x.Id == businessId, ct);

        var capabilities = await _db.Capabilities
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var profileDefaults = await _db.IndustryProfileCapabilities
            .AsNoTracking()
            .Where(x => x.IndustryProfileId == business.IndustryProfileId)
            .ToDictionaryAsync(x => x.CapabilityId, x => x.IsEnabled, ct);

        var businessOverrides = await _db.BusinessCapabilityOverrides
            .AsNoTracking()
            .ToDictionaryAsync(x => x.CapabilityId, x => x.IsEnabled, ct);

        var outletOverrides = outletId.HasValue
            ? await _db.OutletCapabilityOverrides.AsNoTracking()
                .Where(x => x.OutletId == outletId)
                .ToDictionaryAsync(x => x.CapabilityId, x => x.IsEnabled, ct)
            : new Dictionary<Guid, bool>();

        var effective = capabilities.Select(capability =>
        {
            var hasProfile = profileDefaults.TryGetValue(capability.Id, out var profileEnabled);
            var hasBusinessOverride = businessOverrides.TryGetValue(capability.Id, out var businessOverride);
            var businessEnabled = hasBusinessOverride
                ? businessOverride
                : hasProfile ? profileEnabled : capability.DefaultEnabled;

            var outletOverride = false;
            var hasOutletOverride = outletId.HasValue &&
                capability.Scope == CapabilityScopes.Outlet &&
                outletOverrides.TryGetValue(capability.Id, out outletOverride);
            var enabled = businessEnabled && (!hasOutletOverride || outletOverride);

            var source = !businessEnabled && hasOutletOverride && outletOverride
                ? "BusinessDisabled"
                : hasOutletOverride ? "OutletOverride"
                : hasBusinessOverride ? "BusinessOverride"
                : hasProfile ? "IndustryProfile"
                : "CapabilityDefault";

            return new EffectiveCapabilityDto(
                capability.Id,
                capability.Code,
                capability.Name,
                capability.Module,
                capability.Scope,
                enabled,
                businessEnabled,
                hasProfile ? profileEnabled : null,
                hasBusinessOverride ? businessOverride : null,
                hasOutletOverride ? outletOverride : null,
                source,
                capability.IsTenantConfigurable);
        }).ToList();

        var result = new EffectiveCapabilitySnapshot(
            business.Id,
            outletId,
            business.CapabilityRevision,
            new IndustryProfileDto(
                business.IndustryProfile.Id,
                business.IndustryProfile.Code,
                business.IndustryProfile.Name,
                business.IndustryProfile.Description),
            effective);

        _cache[cacheKey] = result;
        return result;
    }

    public async Task<bool> IsEnabledAsync(string capabilityCode, Guid? outletId, CancellationToken ct = default)
    {
        var snapshot = await GetEffectiveAsync(outletId, ct);
        return snapshot.Capabilities.FirstOrDefault(x => x.Code == capabilityCode)?.Enabled ?? false;
    }

    public async Task EnsureEnabledAsync(string capabilityCode, Guid? outletId, CancellationToken ct = default)
    {
        if (!await IsEnabledAsync(capabilityCode, outletId, ct))
            throw new CapabilityDisabledException(capabilityCode, outletId);
    }

    public void Invalidate() => _cache.Clear();
}

public sealed class CapabilityAdminService : ICapabilityAdminService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICapabilityEvaluator _evaluator;

    public CapabilityAdminService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ICapabilityEvaluator evaluator)
    {
        _db = db;
        _currentUser = currentUser;
        _evaluator = evaluator;
    }

    public async Task<IReadOnlyList<IndustryProfileDto>> GetIndustryProfilesAsync(CancellationToken ct = default)
        => await _db.IndustryProfiles.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new IndustryProfileDto(x.Id, x.Code, x.Name, x.Description))
            .ToListAsync(ct);

    public async Task<EffectiveCapabilitySnapshot> UpdateBusinessOverridesAsync(
        UpdateCapabilityOverridesRequest request,
        CancellationToken ct = default)
    {
        EnsureOwner();
        ValidateRequest(request);
        var (business, userId) = await LoadBusinessAsync(request.ExpectedRevision, ct);
        var capabilities = await LoadCapabilitiesAsync(request.Items, CapabilityScopes.Business, allowOutletScope: true, ct);
        var existing = await _db.BusinessCapabilityOverrides
            .Where(x => capabilities.Keys.Contains(x.CapabilityId))
            .ToDictionaryAsync(x => x.CapabilityId, ct);

        var changed = false;
        foreach (var item in request.Items)
        {
            var capability = capabilities.Values.Single(x => x.Code == item.Code);
            existing.TryGetValue(capability.Id, out var row);
            if (item.Enabled is null)
            {
                if (row is null) continue;
                _db.BusinessCapabilityOverrides.Remove(row);
                AddLog(business.Id, null, capability.Id, "BusinessOverrideRemoved", row.IsEnabled, null, request.Reason, userId);
                changed = true;
                continue;
            }

            if (row is null)
            {
                _db.BusinessCapabilityOverrides.Add(new BusinessCapabilityOverride
                {
                    BusinessId = business.Id,
                    CapabilityId = capability.Id,
                    IsEnabled = item.Enabled.Value,
                    Reason = NormalizeReason(request.Reason),
                    UpdatedByUserId = userId,
                });
                AddLog(business.Id, null, capability.Id, "BusinessOverrideSet", null, item.Enabled, request.Reason, userId);
                changed = true;
            }
            else if (row.IsEnabled != item.Enabled || row.Reason != NormalizeReason(request.Reason))
            {
                var old = row.IsEnabled;
                row.IsEnabled = item.Enabled.Value;
                row.Reason = NormalizeReason(request.Reason);
                row.UpdatedByUserId = userId;
                row.UpdatedAt = DateTime.UtcNow;
                AddLog(business.Id, null, capability.Id, "BusinessOverrideSet", old, item.Enabled, request.Reason, userId);
                changed = true;
            }
        }

        await SaveAsync(business, changed, ct);
        return await _evaluator.GetEffectiveAsync(null, ct);
    }

    public async Task<EffectiveCapabilitySnapshot> UpdateOutletOverridesAsync(
        Guid outletId,
        UpdateCapabilityOverridesRequest request,
        CancellationToken ct = default)
    {
        EnsureOwner();
        ValidateRequest(request);
        var (business, userId) = await LoadBusinessAsync(request.ExpectedRevision, ct);
        if (!await _db.Outlets.AnyAsync(x => x.Id == outletId, ct))
            throw new KeyNotFoundException("Outlet tidak ditemukan.");

        var capabilities = await LoadCapabilitiesAsync(request.Items, CapabilityScopes.Outlet, allowOutletScope: false, ct);
        var businessSnapshot = await _evaluator.GetEffectiveAsync(null, ct);
        foreach (var item in request.Items.Where(x => x.Enabled == true))
        {
            if (businessSnapshot.Capabilities.First(x => x.Code == item.Code).BusinessEnabled == false)
                throw new CapabilityParentDisabledException(item.Code);
        }

        var existing = await _db.OutletCapabilityOverrides
            .Where(x => x.OutletId == outletId && capabilities.Keys.Contains(x.CapabilityId))
            .ToDictionaryAsync(x => x.CapabilityId, ct);
        var changed = false;
        foreach (var item in request.Items)
        {
            var capability = capabilities.Values.Single(x => x.Code == item.Code);
            existing.TryGetValue(capability.Id, out var row);
            if (item.Enabled is null)
            {
                if (row is null) continue;
                _db.OutletCapabilityOverrides.Remove(row);
                AddLog(business.Id, outletId, capability.Id, "OutletOverrideRemoved", row.IsEnabled, null, request.Reason, userId);
                changed = true;
                continue;
            }

            if (row is null)
            {
                _db.OutletCapabilityOverrides.Add(new OutletCapabilityOverride
                {
                    BusinessId = business.Id,
                    OutletId = outletId,
                    CapabilityId = capability.Id,
                    IsEnabled = item.Enabled.Value,
                    Reason = NormalizeReason(request.Reason),
                    UpdatedByUserId = userId,
                });
                AddLog(business.Id, outletId, capability.Id, "OutletOverrideSet", null, item.Enabled, request.Reason, userId);
                changed = true;
            }
            else if (row.IsEnabled != item.Enabled || row.Reason != NormalizeReason(request.Reason))
            {
                var old = row.IsEnabled;
                row.IsEnabled = item.Enabled.Value;
                row.Reason = NormalizeReason(request.Reason);
                row.UpdatedByUserId = userId;
                row.UpdatedAt = DateTime.UtcNow;
                AddLog(business.Id, outletId, capability.Id, "OutletOverrideSet", old, item.Enabled, request.Reason, userId);
                changed = true;
            }
        }

        await SaveAsync(business, changed, ct);
        return await _evaluator.GetEffectiveAsync(outletId, ct);
    }

    public async Task<EffectiveCapabilitySnapshot> UpdateIndustryProfileAsync(
        UpdateIndustryProfileRequest request,
        CancellationToken ct = default)
    {
        EnsureOwner();
        var (business, userId) = await LoadBusinessAsync(request.ExpectedRevision, ct);
        var profile = await _db.IndustryProfiles
            .SingleOrDefaultAsync(x => x.Code == request.ProfileCode && x.IsActive, ct)
            ?? throw new InvalidOperationException("Industry profile tidak valid.");
        if (business.IndustryProfileId != profile.Id)
        {
            var oldProfile = business.IndustryProfileId;
            business.IndustryProfileId = profile.Id;
            business.UpdatedAt = DateTime.UtcNow;
            _db.CapabilityChangeLogs.Add(new CapabilityChangeLog
            {
                BusinessId = business.Id,
                ChangeType = "IndustryProfileChanged",
                OldProfileId = oldProfile,
                NewProfileId = profile.Id,
                Reason = NormalizeReason(request.Reason),
                ChangedByUserId = userId,
            });
            await SaveAsync(business, true, ct);
        }
        else
        {
            _evaluator.Invalidate();
        }

        return await _evaluator.GetEffectiveAsync(null, ct);
    }

    public async Task<IReadOnlyList<CapabilityChangeLogDto>> GetHistoryAsync(
        Guid? outletId,
        string? capabilityCode,
        DateTime? from,
        DateTime? to,
        int take,
        CancellationToken ct = default)
    {
        var query = _db.CapabilityChangeLogs.AsNoTracking();
        if (outletId.HasValue) query = query.Where(x => x.OutletId == outletId);
        if (!string.IsNullOrWhiteSpace(capabilityCode))
            query = query.Where(x => x.Capability != null && x.Capability.Code == capabilityCode);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to.Value);

        return await query.OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new CapabilityChangeLogDto(
                x.Id,
                x.OutletId,
                x.Outlet != null ? x.Outlet.Name : null,
                x.Capability != null ? x.Capability.Code : null,
                x.Capability != null ? x.Capability.Name : null,
                x.ChangeType,
                x.OldValue,
                x.NewValue,
                x.Reason,
                x.ChangedByUserId,
                x.ChangedByUser.Name,
                x.CreatedAt))
            .ToListAsync(ct);
    }

    private void EnsureOwner()
    {
        if (_currentUser.Role != "Owner")
            throw new UnauthorizedAccessException("Hanya Owner yang dapat mengubah capability.");
    }

    private static void ValidateRequest(UpdateCapabilityOverridesRequest request)
    {
        if (request.Items.Count == 0) throw new InvalidOperationException("Daftar capability kosong.");
        if (request.Items.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Items.Count)
            throw new InvalidOperationException("Capability duplikat pada request.");
        if (request.Items.Any(x => x.Enabled == false) && string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Alasan wajib diisi saat menonaktifkan capability.");
    }

    private async Task<(Business Business, Guid UserId)> LoadBusinessAsync(long expectedRevision, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("User context wajib tersedia.");
        var business = await _db.Businesses.SingleAsync(ct);
        if (business.CapabilityRevision != expectedRevision)
            throw new CapabilityRevisionConflictException();
        return (business, userId);
    }

    private async Task<Dictionary<Guid, Capability>> LoadCapabilitiesAsync(
        IReadOnlyList<CapabilityOverrideItem> items,
        string requiredScope,
        bool allowOutletScope,
        CancellationToken ct)
    {
        var codes = items.Select(x => x.Code).ToArray();
        var capabilities = await _db.Capabilities
            .Where(x => codes.Contains(x.Code) && x.IsActive && x.IsTenantConfigurable)
            .ToDictionaryAsync(x => x.Id, ct);
        if (capabilities.Count != codes.Length)
            throw new InvalidOperationException("Satu atau lebih capability tidak valid atau tidak dapat dikonfigurasi.");
        if (!allowOutletScope && capabilities.Values.Any(x => x.Scope != requiredScope))
            throw new InvalidOperationException("Hanya capability scope Outlet yang dapat diubah pada Outlet.");
        return capabilities;
    }

    private void AddLog(
        Guid businessId,
        Guid? outletId,
        Guid capabilityId,
        string changeType,
        bool? oldValue,
        bool? newValue,
        string? reason,
        Guid userId)
        => _db.CapabilityChangeLogs.Add(new CapabilityChangeLog
        {
            BusinessId = businessId,
            OutletId = outletId,
            CapabilityId = capabilityId,
            ChangeType = changeType,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = NormalizeReason(reason),
            ChangedByUserId = userId,
        });

    private async Task SaveAsync(Business business, bool changed, CancellationToken ct)
    {
        if (!changed)
        {
            _evaluator.Invalidate();
            return;
        }

        business.CapabilityRevision++;
        business.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CapabilityRevisionConflictException();
        }
        _evaluator.Invalidate();
    }

    private static string? NormalizeReason(string? reason)
        => string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}
