using Microsoft.EntityFrameworkCore;

namespace MorrusPOS.Infrastructure.Persistence;

/// <summary>
/// Context for deliberately cross-tenant operations such as authentication and provisioning.
/// It must use the restricted system connection, never the normal application connection.
/// </summary>
public sealed class SystemDbContext : AppDbContext
{
    public SystemDbContext(DbContextOptions<SystemDbContext> options)
        : base(options, true)
    {
    }
}
