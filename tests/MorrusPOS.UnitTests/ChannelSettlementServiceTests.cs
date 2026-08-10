using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ChannelSettlementServiceTests
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserServiceMock;

    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _roleId = Guid.NewGuid();
    private readonly Guid _channelAccountId = Guid.NewGuid();

    public ChannelSettlementServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserServiceMock = Substitute.For<ICurrentUserService>();
        _currentUserServiceMock.Role.Returns("Owner");
        _currentUserServiceMock.OutletId.Returns((Guid?)null);
        _currentUserServiceMock.UserId.Returns(_userId);
    }

    private async Task<(Guid firstId, Guid secondId)> SeedDataAsync()
    {
        _dbContext.Outlets.Add(new Outlet { Id = _outletId, Code = "OUT-CH", Name = "Outlet Channel", IsActive = true });
        _dbContext.Users.Add(new User { Id = _userId, Name = "Finance", Email = "finance@channel.test", PasswordHash = "hash", RoleId = _roleId });
        _dbContext.ChannelAccounts.Add(new ChannelAccount
        {
            Id = _channelAccountId,
            OutletId = _outletId,
            Name = "GoFood Outlet A",
            ChannelName = TransactionChannel.GoFood,
            MerchantId = "GF-001",
            DefaultCommissionRate = 20,
            IsActive = true
        });

        var firstTransactionId = Guid.NewGuid();
        var secondTransactionId = Guid.NewGuid();

        _dbContext.Transactions.AddRange(
            new Transaction
            {
                Id = firstTransactionId,
                OutletId = _outletId,
                UserId = _userId,
                TransactionNumber = "TRX-CH-001",
                Channel = TransactionChannel.GoFood,
                Status = TransactionStatus.Completed,
                Subtotal = 100000,
                DiscountTotal = 0,
                TaxTotal = 0,
                GrandTotal = 100000,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Transaction
            {
                Id = secondTransactionId,
                OutletId = _outletId,
                UserId = _userId,
                TransactionNumber = "TRX-CH-002",
                Channel = TransactionChannel.GoFood,
                Status = TransactionStatus.Completed,
                Subtotal = 50000,
                DiscountTotal = 0,
                TaxTotal = 0,
                GrandTotal = 50000,
                CreatedAt = DateTime.UtcNow.AddHours(-12)
            });

        await _dbContext.SaveChangesAsync();
        return (firstTransactionId, secondTransactionId);
    }

    [Fact]
    public async Task CreateAsync_Should_CreatePendingSettlementWithCalculatedNet()
    {
        var (firstId, secondId) = await SeedDataAsync();
        var service = new ChannelSettlementService(_dbContext, _currentUserServiceMock);

        var result = await service.CreateAsync(_userId, new CreateChannelSettlementRequest(
            _channelAccountId,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow,
            null,
            new List<Guid> { firstId, secondId }
        ));

        result.Status.Should().Be("pending");
        result.GrossAmount.Should().Be(150000);
        result.CommissionAmount.Should().Be(30000);
        result.NetAmount.Should().Be(120000);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_Should_RejectTransactionAlreadyUsedByAnotherSettlement()
    {
        var (firstId, secondId) = await SeedDataAsync();
        var service = new ChannelSettlementService(_dbContext, _currentUserServiceMock);

        await service.CreateAsync(_userId, new CreateChannelSettlementRequest(
            _channelAccountId,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow,
            null,
            new List<Guid> { firstId }
        ));

        var act = () => service.CreateAsync(_userId, new CreateChannelSettlementRequest(
            _channelAccountId,
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow,
            null,
            new List<Guid> { firstId, secondId }
        ));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tidak valid*");
    }
}
