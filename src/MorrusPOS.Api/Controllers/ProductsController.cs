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
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

    public ProductsController(
        IProductService productService,
        ICurrentUserService currentUser,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _productService = productService;
        _currentUser = currentUser;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(
        [FromQuery] Guid? outletId,
        [FromQuery] bool? isRawMaterial,
        CancellationToken ct)
    {
        // Shortcut: kalau minta bahan baku, tidak perlu outletId
        if (isRawMaterial == true)
        {
            var rawMaterials = await _productService.GetRawMaterialsAsync(ct);
            return Ok(rawMaterials);
        }

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

    [HttpPost("upload-image")]
    [HasPermission("product.manage")]
    public async Task<IActionResult> UploadImage(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File tidak boleh kosong.");
        }

        // Validate file size (max 2MB)
        if (file.Length > 2 * 1024 * 1024)
        {
            return BadRequest("Ukuran file tidak boleh melebihi 2MB.");
        }

        // Validate extensions
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest("Hanya diperbolehkan format PNG, JPG, JPEG, atau WEBP.");
        }

        // Target path: wwwroot/uploads/
        var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"/uploads/{fileName}";
        return Ok(new { url = relativeUrl });
    }
}
