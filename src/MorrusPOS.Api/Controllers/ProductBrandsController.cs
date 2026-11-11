using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController, Route("api/product-brands"), Authorize, RequiresCapability(CapabilityCodes.CatalogProducts)]
public class ProductBrandsController(IProductCatalogMasterService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(bool includeInactive = false, string? search = null, CancellationToken ct = default)
        => Ok(await service.GetBrandsAsync(includeInactive, search, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetBrandAsync(id, ct));
    [HttpPost, HasPermission("product.manage")]
    public async Task<IActionResult> Create(SaveProductBrandRequest request, CancellationToken ct)
    {
        var result = await service.SaveBrandAsync(null, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPut("{id:guid}"), HasPermission("product.manage")]
    public async Task<IActionResult> Update(Guid id, SaveProductBrandRequest request, CancellationToken ct)
        => Ok(await service.SaveBrandAsync(id, request, ct));
    [HttpDelete("{id:guid}"), HasPermission("product.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteBrandAsync(id, ct);
        return NoContent();
    }
}
