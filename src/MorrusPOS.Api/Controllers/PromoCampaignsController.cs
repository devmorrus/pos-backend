using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Pricing;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/promo-campaigns")]
[Authorize(Roles = "Owner,Admin,Keuangan")]
[HasPermission("pricing.manage")]
public class PromoCampaignsController : ControllerBase
{
    private readonly IPricingAdminService _pricingAdminService;

    public PromoCampaignsController(IPricingAdminService pricingAdminService)
    {
        _pricingAdminService = pricingAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromoCampaignDto>>> GetAll([FromQuery] Guid? outletId, CancellationToken ct)
        => Ok(await _pricingAdminService.GetPromoCampaignsAsync(outletId, ct));

    [HttpPost]
    public async Task<ActionResult<PromoCampaignDto>> Create(CreatePromoCampaignRequest request, CancellationToken ct)
    {
        var result = await _pricingAdminService.CreatePromoCampaignAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { outletId = result.OutletId }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PromoCampaignDto>> Update(Guid id, UpdatePromoCampaignRequest request, CancellationToken ct)
        => Ok(await _pricingAdminService.UpdatePromoCampaignAsync(id, request, ct));
}
