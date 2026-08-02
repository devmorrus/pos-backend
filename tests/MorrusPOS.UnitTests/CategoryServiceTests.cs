using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class CategoryServiceTests
{
    private readonly AppDbContext _dbContext;

    public CategoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_Should_CreateCategory_When_Valid()
    {
        // Arrange
        var service = new CategoryService(_dbContext);
        var request = new CreateCategoryRequest("Snack", null);

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Snack");

        var saved = await _dbContext.Categories.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Snack");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_When_NameDuplicate()
    {
        // Arrange
        _dbContext.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Snack" });
        await _dbContext.SaveChangesAsync();

        var service = new CategoryService(_dbContext);
        var request = new CreateCategoryRequest("snack", null); // case-insensitive check

        // Act & Assert
        var act = () => service.CreateAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Nama kategori sudah digunakan.");
    }

    [Fact]
    public async Task UpdateAsync_Should_ThrowException_OnCircularParentReference()
    {
        // Arrange
        var catId = Guid.NewGuid();
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        await _dbContext.SaveChangesAsync();

        var service = new CategoryService(_dbContext);
        var request = new UpdateCategoryRequest("Snack", catId); // parent set to self

        // Act & Assert
        var act = () => service.UpdateAsync(catId, request);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Kategori tidak boleh menjadi induk dari dirinya sendiri.");
    }

    [Fact]
    public async Task DeleteAsync_Should_ThrowException_When_CategoryHasProducts()
    {
        // Arrange
        var catId = Guid.NewGuid();
        _dbContext.Categories.Add(new Category { Id = catId, Name = "Snack" });
        _dbContext.Products.Add(new Product { Id = Guid.NewGuid(), CategoryId = catId, Sku = "SKU-1", Name = "Product 1", Unit = "pcs", IsActive = true });
        await _dbContext.SaveChangesAsync();

        var service = new CategoryService(_dbContext);

        // Act & Assert
        var act = () => service.DeleteAsync(catId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Kategori tidak bisa dihapus karena masih digunakan oleh produk aktif.");
    }
}
