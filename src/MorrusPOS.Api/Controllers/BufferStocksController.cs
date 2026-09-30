using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BufferStocksController : ControllerBase
{
    private readonly IBufferStockService _bufferStockService;
    private readonly ICurrentUserService _currentUser;

    public BufferStocksController(
        IBufferStockService bufferStockService,
        ICurrentUserService currentUser)
    {
        _bufferStockService = bufferStockService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<IReadOnlyList<BufferStockListItemDto>>> GetByOutlet(
        [FromQuery] Guid? outletId,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat buffer stock.");
        }

        var result = await _bufferStockService.GetByOutletAsync(
            resolvedOutletId.Value,
            search,
            ct);

        return Ok(result);
    }

    [HttpGet("availability")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<OnlineStockAvailabilityDto>> GetAvailability(
        [FromQuery] Guid? outletId,
        [FromQuery] Guid productId,
        [FromQuery] Guid? productVariantId,
        CancellationToken ct = default)
    {
        var resolvedOutletId = ResolveTargetOutletId(outletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk melihat ketersediaan online.");
        }

        if (productId == Guid.Empty)
        {
            return BadRequest("Product ID wajib diisi.");
        }

        var result = await _bufferStockService.GetAvailabilityAsync(
            resolvedOutletId.Value,
            productId,
            productVariantId,
            ct);

        return Ok(result);
    }

    [HttpPut("{productId:guid}")]
    [HasPermission("stock.manage")]
    public async Task<ActionResult<BufferStockListItemDto>> Upsert(
        Guid productId,
        [FromBody] UpsertBufferStockRequest request,
        CancellationToken ct = default)
    {
        if (productId == Guid.Empty)
        {
            return BadRequest("Product ID tidak valid.");
        }

        if (request.ProductId != Guid.Empty && request.ProductId != productId)
        {
            return BadRequest("Product ID pada path dan body tidak cocok.");
        }

        var effectiveRequest = request with { ProductId = productId };

        var resolvedOutletId = ResolveTargetOutletId(effectiveRequest.OutletId);
        if (resolvedOutletId == null)
        {
            return BadRequest("Pilih outlet terlebih dahulu untuk mengatur buffer stock.");
        }

        effectiveRequest = effectiveRequest with { OutletId = resolvedOutletId.Value };

        if (_currentUser.UserId == null || _currentUser.UserId == Guid.Empty)
        {
            return Unauthorized("User tidak valid.");
        }

        var result = await _bufferStockService.UpsertAsync(
            _currentUser.UserId.Value,
            effectiveRequest,
            ct);
        return Ok(result);
    }

    private Guid? ResolveTargetOutletId(Guid? requestedOutletId)
    {
        if (_currentUser.Role == "Owner")
        {
            // Owner boleh akses semua outlet, tapi wajib memilih satu outlet.
            if (requestedOutletId == null || requestedOutletId == Guid.Empty)
            {
                return null;
            }

            return requestedOutletId;
        }

        if (requestedOutletId.HasValue && requestedOutletId != Guid.Empty &&
            requestedOutletId != _currentUser.OutletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }

        return _currentUser.OutletId;
    }
}
