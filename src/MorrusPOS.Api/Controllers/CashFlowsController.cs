using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Features.Accounting;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/cash-flows")]
[Authorize]
public class CashFlowsController : ControllerBase
{
    private readonly ICashFlowService _cashFlowService;
    private readonly IWebHostEnvironment _env;

    public CashFlowsController(ICashFlowService cashFlowService, IWebHostEnvironment env)
    {
        _cashFlowService = cashFlowService;
        _env = env;
    }

    [HttpGet]
    [HasPermission("cashflow.view")]
    public async Task<ActionResult<IReadOnlyList<CashFlowListItemDto>>> Get([FromQuery] CashFlowFilters filters, CancellationToken ct)
    {
        var result = await _cashFlowService.GetAsync(filters, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("cashflow.view")]
    public async Task<ActionResult<CashFlowDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _cashFlowService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("income-business")]
    [HasPermission("cashflow.create")]
    public async Task<ActionResult<CashFlowDetailDto>> CreateIncome(CreateBusinessIncomeRequest request, CancellationToken ct)
    {
        var result = await _cashFlowService.CreateIncomeAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("outcome-business")]
    [HasPermission("cashflow.create")]
    public async Task<ActionResult<CashFlowDetailDto>> CreateOutcome(CreateBusinessOutcomeRequest request, CancellationToken ct)
    {
        var result = await _cashFlowService.CreateOutcomeAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("upload-attachment")]
    [HasPermission("cashflow.create")]
    public async Task<IActionResult> UploadAttachment(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File tidak boleh kosong.");
        }

        if (file.Length > 2 * 1024 * 1024)
        {
            return BadRequest("Ukuran file tidak boleh melebihi 2MB.");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest("Hanya diperbolehkan format JPG, JPEG, PNG, atau PDF.");
        }

        var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "cash-flows");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        var relativeUrl = $"/uploads/cash-flows/{fileName}";
        return Ok(new { url = relativeUrl });
    }
}
