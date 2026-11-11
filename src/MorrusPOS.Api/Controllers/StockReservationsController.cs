using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/stock-reservations")]
[Authorize]
public sealed class StockReservationsController : ControllerBase
{
    private readonly IStockReservationService _service;
    private readonly StockLocationAccessResolver _locations;

    public StockReservationsController(IStockReservationService service, StockLocationAccessResolver locations)
    {
        _service = service;
        _locations = locations;
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Kasir,KepalaCabang")]
    [RequiresCapability(CapabilityCodes.SalesPos)]
    public async Task<ActionResult<StockReservationDto>> Create(
        CreateStockReservationRequest request, CancellationToken ct)
    {
        ValidateIdempotencyHeader(request.Id);
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockReservationDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet]
    [HasPermission("stock.manage")]
    [RequiresCapability(CapabilityCodes.InventoryStock)]
    public async Task<ActionResult<IReadOnlyList<StockReservationDto>>> GetActive(
        [FromQuery] Guid? stockLocationId,
        [FromQuery] Guid? outletId,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? productVariantId,
        CancellationToken ct)
    {
        var locationId = await _locations.ResolveAsync(stockLocationId, outletId, ct);
        return Ok(await _service.GetActiveAsync(locationId, productId, productVariantId, ct));
    }

    [HttpPost("{id:guid}/release")]
    [Authorize(Roles = "Owner,Admin,Kasir,KepalaCabang")]
    public async Task<ActionResult<StockReservationDto>> Release(Guid id, CancellationToken ct)
        => Ok(await _service.ReleaseAsync(id, "Dilepas oleh pengguna.", ct));

    private void ValidateIdempotencyHeader(Guid requestId)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var value)) return;
        if (!Guid.TryParse(value.ToString(), out var headerId) || headerId == Guid.Empty)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyInvalid,
                "Idempotency-Key harus berupa UUID valid.");
        if (requestId != headerId)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyReused,
                "Idempotency-Key header tidak sama dengan reservation ID.");
    }
}
