using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(CancellationToken ct)
    {
        // OutletId null (Owner) -> IProductService implementasi wajib handle
        // kasus ini secara eksplisit (mis. minta outletId sebagai query param).
        if (_currentUser.OutletId is null)
            return BadRequest("Owner wajib menentukan outlet_id lewat query parameter untuk endpoint ini.");

        var result = await _productService.GetByOutletAsync(_currentUser.OutletId.Value, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var result = await _productService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }
}
