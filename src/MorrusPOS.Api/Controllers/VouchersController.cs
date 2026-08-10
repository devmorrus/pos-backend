using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Pricing;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/vouchers")]
[Authorize(Roles = "Owner,Admin,Keuangan")]
[HasPermission("pricing.manage")]
public class VouchersController : ControllerBase
{
    private readonly IPricingAdminService _pricingAdminService;

    public VouchersController(IPricingAdminService pricingAdminService)
    {
        _pricingAdminService = pricingAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VoucherDto>>> GetAll([FromQuery] Guid? outletId, CancellationToken ct)
        => Ok(await _pricingAdminService.GetVouchersAsync(outletId, ct));

    [HttpPost]
    public async Task<ActionResult<VoucherDto>> Create(CreateVoucherRequest request, CancellationToken ct)
    {
        var result = await _pricingAdminService.CreateVoucherAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { outletId = result.OutletId }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<VoucherDto>> Update(Guid id, UpdateVoucherRequest request, CancellationToken ct)
        => Ok(await _pricingAdminService.UpdateVoucherAsync(id, request, ct));

    [HttpPost("{id}/activate")]
    public async Task<ActionResult<VoucherDto>> Activate(Guid id, CancellationToken ct)
        => Ok(await _pricingAdminService.SetVoucherActiveAsync(id, true, ct));

    [HttpPost("{id}/deactivate")]
    public async Task<ActionResult<VoucherDto>> Deactivate(Guid id, CancellationToken ct)
        => Ok(await _pricingAdminService.SetVoucherActiveAsync(id, false, ct));
}
