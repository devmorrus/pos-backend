using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Products;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ICurrentUserService _currentUser;

    public ProductsController(IProductService productService, ICurrentUserService currentUser)
    {
        _productService = productService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll([FromQuery] Guid? outletId, CancellationToken ct)
    {
        var targetOutletId = outletId ?? _currentUser.OutletId;
        if (targetOutletId is null)
        {
            return BadRequest("Wajib menentukan outlet_id lewat query parameter.");
        }

        var result = await _productService.GetByOutletAsync(targetOutletId.Value, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _productService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("product.manage")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var result = await _productService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [HasPermission("product.manage")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var result = await _productService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [HasPermission("product.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        return NoContent();
    }
}
