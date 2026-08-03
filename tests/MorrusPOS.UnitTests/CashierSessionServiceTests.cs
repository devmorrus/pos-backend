using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class CashierSessionServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserMock;

    public CashierSessionServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = Substitute.For<ICurrentUserService>();
    }

    [Fact]
    public async Task OpenSessionAsync_Should_OpenNewSession_When_NoneActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.Role.Returns("Kasir");
        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);

        var service = new CashierSessionService(_dbContext, _currentUserMock);
        var request = new OpenSessionRequest(OpeningCash: 100000);

        // Act
        var result = await service.OpenSessionAsync(userId, outletId, request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("open");
        result.OpeningCash.Should().Be(100000);
        result.ExpectedCash.Should().Be(100000);
    }

    [Fact]
    public async Task OpenSessionAsync_Should_ThrowException_When_SessionAlreadyActive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.CashierSessions.Add(new CashierSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OutletId = outletId,
            OpeningCash = 50000,
            Status = "open",
            OpeningTime = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.Role.Returns("Kasir");
        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);

        var service = new CashierSessionService(_dbContext, _currentUserMock);
        var request = new OpenSessionRequest(OpeningCash: 100000);

        // Act & Assert
        var act = () => service.OpenSessionAsync(userId, outletId, request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Anda masih memiliki sesi kasir yang aktif di outlet ini. Harap tutup sesi terlebih dahulu.");
    }

    [Fact]
    public async Task CloseSessionAsync_Should_CalculateExpectedCashAndVariance_FromCashSales()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var outletId = Guid.NewGuid();

        _dbContext.Outlets.Add(new Outlet { Id = outletId, Code = "OUT-1", Name = "Main Outlet", IsActive = true });
        _dbContext.Users.Add(new User { Id = userId, Name = "Kasir 1", Email = "k1@morruspos.com", PasswordHash = "hash", RoleId = Guid.NewGuid() });
        _dbContext.CashierSessions.Add(new CashierSession
        {
            Id = sessionId,
            UserId = userId,
            OutletId = outletId,
            OpeningCash = 100000,
            Status = "open",
            OpeningTime = DateTime.UtcNow
        });

        // Add completed transaction in this session with Cash payment of 50000
        var trxId = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = trxId,
            OutletId = outletId,
            UserId = userId,
            CashierSessionId = sessionId,
            TransactionNumber = "TRX-1",
            Channel = "pos",
            Status = "completed",
            GrandTotal = 50000,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Transactions.Add(transaction);
        _dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TransactionId = trxId,
            Method = "cash",
            Amount = 50000,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.Role.Returns("Kasir");
        _currentUserMock.UserId.Returns(userId);
        _currentUserMock.OutletId.Returns(outletId);

        var service = new CashierSessionService(_dbContext, _currentUserMock);
        var request = new CloseSessionRequest(ActualCash: 148000); // 100000 + 50000 = 150000 expected. actual = 148000. variance should be -2000.

        // Act
        var result = await service.CloseSessionAsync(sessionId, request);

        // Assert
        result.Status.Should().Be("closed");
        result.ExpectedCash.Should().Be(150000);
        result.ActualCash.Should().Be(148000);
        result.Variance.Should().Be(-2000);
    }
}
