using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController, Route("api/product-models"), Authorize, RequiresCapability(CapabilityCodes.CatalogProducts)]
public class ProductModelsController(IProductCatalogMasterService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid? brandId = null, bool includeInactive = false, string? search = null, CancellationToken ct = default)
        => Ok(await service.GetModelsAsync(brandId, includeInactive, search, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetModelAsync(id, ct));
    [HttpPost, HasPermission("product.manage")]
    public async Task<IActionResult> Create(SaveProductModelRequest request, CancellationToken ct)
    {
        var result = await service.SaveModelAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPut("{id:guid}"), HasPermission("product.manage")]
    public async Task<IActionResult> Update(Guid id, SaveProductModelRequest request, CancellationToken ct)
        => Ok(await service.SaveModelAsync(id, request, ct));
    [HttpDelete("{id:guid}"), HasPermission("product.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteModelAsync(id, ct);
        return NoContent();
    }
}
