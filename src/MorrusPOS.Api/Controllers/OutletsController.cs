using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin")]
public class OutletsController : ControllerBase
{
    private readonly IRepository<Outlet> _outletRepository;

    public OutletsController(IUnitOfWork unitOfWork)
    {
        _outletRepository = unitOfWork.Repository<Outlet>();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OutletLookupDto>>> GetAll(CancellationToken ct)
    {
        var outlets = await _outletRepository.ListAsync(null, ct);
        var result = outlets.Select(o => new OutletLookupDto(o.Id, o.Code, o.Name, o.IsActive)).ToList();
        return Ok(result);
    }
}

public record OutletLookupDto(Guid Id, string Code, string Name, bool IsActive);
