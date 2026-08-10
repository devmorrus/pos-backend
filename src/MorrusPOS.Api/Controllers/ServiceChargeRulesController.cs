using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Pricing;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/service-charge-rules")]
[Authorize(Roles = "Owner,Admin,Keuangan")]
[HasPermission("pricing.manage")]
public class ServiceChargeRulesController : ControllerBase
{
    private readonly IPricingAdminService _pricingAdminService;

    public ServiceChargeRulesController(IPricingAdminService pricingAdminService)
    {
        _pricingAdminService = pricingAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceChargeRuleDto>>> GetAll([FromQuery] Guid? outletId, CancellationToken ct)
        => Ok(await _pricingAdminService.GetServiceChargeRulesAsync(outletId, ct));

    [HttpPost]
    public async Task<ActionResult<ServiceChargeRuleDto>> Create(CreateServiceChargeRuleRequest request, CancellationToken ct)
    {
        var result = await _pricingAdminService.CreateServiceChargeRuleAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { outletId = result.OutletId }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ServiceChargeRuleDto>> Update(Guid id, UpdateServiceChargeRuleRequest request, CancellationToken ct)
        => Ok(await _pricingAdminService.UpdateServiceChargeRuleAsync(id, request, ct));
}
