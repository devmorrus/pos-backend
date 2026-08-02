using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Products;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
    {
        var result = await _categoryService.GetAllAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _categoryService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("product.manage")]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await _categoryService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [HasPermission("product.manage")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var result = await _categoryService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [HasPermission("product.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _categoryService.DeleteAsync(id, ct);
        return NoContent();
    }
}
