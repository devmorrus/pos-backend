using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Users;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class UserServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasherMock;
    private readonly ICurrentUserService _currentUserMock;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _passwordHasherMock = Substitute.For<IPasswordHasher>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateUser_When_RequestIsValid()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var outletId = Guid.NewGuid();

        _dbContext.Roles.Add(new Role { Id = roleId, Name = "Kasir" });
        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-01", Name = "Outlet 1", IsActive = true });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.Role.Returns("Owner");
        _passwordHasherMock.Hash(Arg.Any<string>()).Returns("hashed_password");

        var service = new UserService(_dbContext, _passwordHasherMock, _currentUserMock);
        var request = new CreateUserRequest(
            OutletId: outletId,
            RoleId: roleId,
            Name: "Budi Kasir",
            Email: "budi@morruspos.com",
            Password: "password123"
        );

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Budi Kasir");
        result.Email.Should().Be("budi@morruspos.com");

        var savedUser = await _dbContext.Users.FindAsync(result.Id);
        savedUser.Should().NotBeNull();
        savedUser!.PasswordHash.Should().Be("hashed_password");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_When_EmailIsDuplicate()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        _dbContext.Roles.Add(new Role { Id = roleId, Name = "Kasir" });
        _dbContext.Users.Add(new User 
        { 
            Id = Guid.NewGuid(), 
            Name = "Existing", 
            Email = "duplicate@morruspos.com", 
            PasswordHash = "hash", 
            RoleId = roleId 
        });
        await _dbContext.SaveChangesAsync();

        var service = new UserService(_dbContext, _passwordHasherMock, _currentUserMock);
        var request = new CreateUserRequest(
            OutletId: null,
            RoleId: roleId,
            Name: "New",
            Email: "duplicate@morruspos.com",
            Password: "password123"
        );

        // Act & Assert
        var act = () => service.CreateAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Email sudah terdaftar*");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_When_NonOwnerAssignsOwnerRole()
    {
        // Arrange
        var ownerRoleId = Guid.NewGuid();
        var adminOutletId = Guid.NewGuid();

        _dbContext.Roles.Add(new Role { Id = ownerRoleId, Name = "Owner" });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.Role.Returns("Admin");
        _currentUserMock.OutletId.Returns(adminOutletId);

        var service = new UserService(_dbContext, _passwordHasherMock, _currentUserMock);
        var request = new CreateUserRequest(
            OutletId: adminOutletId,
            RoleId: ownerRoleId,
            Name: "New Owner",
            Email: "newowner@morruspos.com",
            Password: "password123"
        );

        // Act & Assert
        var act = () => service.CreateAsync(request);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
