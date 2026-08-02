using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin")]
public class RolesController : ControllerBase
{
    private readonly IRepository<Role> _roleRepository;

    public RolesController(IUnitOfWork unitOfWork)
    {
        _roleRepository = unitOfWork.Repository<Role>();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleLookupDto>>> GetAll(CancellationToken ct)
    {
        var roles = await _roleRepository.ListAsync(null, ct);
        var result = roles.Select(r => new RoleLookupDto(r.Id, r.Name, r.Description)).ToList();
        return Ok(result);
    }
}

public record RoleLookupDto(Guid Id, string Name, string? Description);
