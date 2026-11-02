using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Api.Security;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/capabilities")]
[Authorize]
public class CapabilitiesController : ControllerBase
{
    private readonly ICapabilityEvaluator _evaluator;
    private readonly ICapabilityAdminService _admin;

    public CapabilitiesController(
        ICapabilityEvaluator evaluator,
        ICapabilityAdminService admin)
    {
        _evaluator = evaluator;
        _admin = admin;
    }

    [HttpGet("effective")]
    public async Task<ActionResult<EffectiveCapabilitySnapshot>> GetEffective(
        [FromQuery] Guid? outletId,
        CancellationToken ct)
        => Ok(await _evaluator.GetEffectiveAsync(outletId, ct));

    [HttpGet("configuration")]
    [HasPermission("capability.view")]
    public async Task<ActionResult<EffectiveCapabilitySnapshot>> GetConfiguration(
        [FromQuery] Guid? outletId,
        CancellationToken ct)
        => Ok(await _evaluator.GetEffectiveAsync(outletId, ct));

    [HttpPut("business-overrides")]
    [HasPermission("capability.manage")]
    public async Task<ActionResult<EffectiveCapabilitySnapshot>> UpdateBusinessOverrides(
        UpdateCapabilityOverridesRequest request,
        CancellationToken ct)
        => Ok(await _admin.UpdateBusinessOverridesAsync(request, ct));

    [HttpPut("outlets/{outletId:guid}/overrides")]
    [HasPermission("capability.manage")]
    public async Task<ActionResult<EffectiveCapabilitySnapshot>> UpdateOutletOverrides(
        Guid outletId,
        UpdateCapabilityOverridesRequest request,
        CancellationToken ct)
        => Ok(await _admin.UpdateOutletOverridesAsync(outletId, request, ct));

    [HttpPut("industry-profile")]
    [HasPermission("capability.manage")]
    public async Task<ActionResult<EffectiveCapabilitySnapshot>> UpdateIndustryProfile(
        UpdateIndustryProfileRequest request,
        CancellationToken ct)
        => Ok(await _admin.UpdateIndustryProfileAsync(request, ct));

    [HttpGet("history")]
    [HasPermission("capability.view")]
    public async Task<ActionResult<IReadOnlyList<CapabilityChangeLogDto>>> GetHistory(
        [FromQuery] Guid? outletId,
        [FromQuery] string? capabilityCode,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
        => Ok(await _admin.GetHistoryAsync(outletId, capabilityCode, from, to, take, ct));
}
