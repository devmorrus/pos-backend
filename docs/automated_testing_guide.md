# Panduan Automation Testing di .NET Core - MorrusPOS

Dokumen ini menjelaskan rancangan arsitektur dan langkah-langkah implementasi **Automation Testing** (Unit Testing & Integration/API Testing) yang dapat kita pasang pada proyek MorrusPOS untuk menjamin keandalan sistem berskala besar.

---

## 🧪 1. Jenis Testing yang Direkomendasikan

Dalam Clean Architecture, kita membagi testing menjadi dua kategori utama:

```
┌────────────────────────────────────────────────────────┐
│               MorrusPOS.IntegrationTests               │  <-- Menguji API End-to-End, Middleware,
│            (Menggunakan WebApplicationFactory)         │      Routing, & Database riil.
└───────────────────────────┬────────────────────────────┘
                            ▼
┌────────────────────────────────────────────────────────┐
│                MorrusPOS.UnitTests                     │  <-- Menguji Business Logic (Services) secara
│            (Menggunakan xUnit & Mocks/NSubstitute)     │      terisolasi dengan Db In-Memory.
└────────────────────────────────────────────────────────┘
```

### A. Unit Testing (MorrusPOS.UnitTests)
* **Tujuan**: Menguji logika bisnis di layer Service (seperti `ProductService` dan `UserService`) secara terisolasi tanpa memanggil database asli atau API layer.
* **Alat**: 
  * **xUnit**: Runner pengujian standar.
  * **NSubstitute**: Pustaka modern untuk membuat objek palsu (Mock) dari interface.
  * **EF Core SQLite In-Memory**: Menggunakan SQLite di memori komputer sebagai database tiruan agar pengujian database sangat cepat.
  * **FluentAssertions**: Menulis assertion pengujian dengan gaya bahasa natural (contoh: `result.Name.Should().Be("Chiki")`).

### B. Integration & API Testing (MorrusPOS.IntegrationTests)
* **Tujuan**: Menguji seluruh alur request-response API mulai dari Middleware (Autentikasi, Tenant Guard), Routing, validasi, logika Service, hingga query database sesungguhnya.
* **Alat**:
  * **Microsoft.AspNetCore.Mvc.Testing**: Menyediakan `WebApplicationFactory` untuk menjalankan server web in-memory yang bisa kita tembak menggunakan `HttpClient` tiruan.

---

## 🛠️ 2. Cara Menyiapkan Proyek Testing

Jalankan perintah-perintah berikut di terminal Anda untuk membuat proyek pengujian dan memasukkannya ke dalam Solution (`MorrusPOS.sln`):

```bash
# 1. Buat folder tests di root proyek
mkdir tests
cd tests

# 2. Buat proyek Unit Tests dan Integration Tests
dotnet new xunit -n MorrusPOS.UnitTests
dotnet new xunit -n MorrusPOS.IntegrationTests

# 3. Masukkan proyek ke dalam Solution
cd ..
dotnet sln add tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj
dotnet sln add tests/MorrusPOS.IntegrationTests/MorrusPOS.IntegrationTests.csproj

# 4. Tambahkan referensi proyek (Dependencies)
# Unit Tests membutuhkan referensi ke Application dan Infrastructure
dotnet add tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj reference src/MorrusPOS.Application/MorrusPOS.Application.csproj
dotnet add tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj reference src/MorrusPOS.Infrastructure/MorrusPOS.Infrastructure.csproj

# Integration Tests membutuhkan referensi ke API
dotnet add tests/MorrusPOS.IntegrationTests/MorrusPOS.IntegrationTests.csproj reference src/MorrusPOS.Api/MorrusPOS.Api.csproj

# 5. Instal paket library pendukung
dotnet add tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj package NSubstitute
dotnet add tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj package FluentAssertions
dotnet add tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj package Microsoft.EntityFrameworkCore.Sqlite

dotnet add tests/MorrusPOS.IntegrationTests/MorrusPOS.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/MorrusPOS.IntegrationTests/MorrusPOS.IntegrationTests.csproj package FluentAssertions
dotnet add tests/MorrusPOS.IntegrationTests/MorrusPOS.IntegrationTests.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

---

## 📝 3. Contoh Implementasi Kode Testing

### A. Contoh Unit Test (`ProductServiceTests.cs`)
Menguji apakah pembuatan produk berhasil memicu pembuatan stok awal sebesar `0` dan menolak SKU ganda.

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ProductServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserMock;
    private readonly SqliteConnection _connection;

    public ProductServiceTests()
    {
        // Setup SQLite In-Memory agar database bersih setiap test dijalankan
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        _currentUserMock = Substitute.For<ICurrentUserService>();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateProduct_And_SeedZeroStockForAllOutlets()
    {
        // Arrange (Persiapan Data & Mock)
        var outletId1 = Guid.NewGuid();
        var outletId2 = Guid.NewGuid();
        _dbContext.Outlets.AddRange(
            new Outlet { Id = outletId1, Code = "OUT-01", Name = "Outlet 1", IsActive = true },
            new Outlet { Id = outletId2, Code = "OUT-02", Name = "Outlet 2", IsActive = true }
        );
        var categoryId = Guid.NewGuid();
        _dbContext.Categories.Add(new Category { Id = categoryId, Name = "Snack" });
        await _dbContext.SaveChangesAsync();

        _currentUserMock.UserId.Returns(Guid.NewGuid());
        _currentUserMock.OutletId.Returns(outletId1);

        var service = new ProductService(_dbContext, _currentUserMock);
        var request = new CreateProductRequest(
            CategoryId: categoryId,
            Sku: "SNK-CHIKI-001",
            Name: "Chiki Balls",
            Barcode: "123456",
            BasePrice: 10000,
            CostPrice: 8000,
            Unit: "pcs",
            IsConsignment: false
        );

        // Act (Jalankan Aksi)
        var result = await service.CreateAsync(request);

        // Assert (Verifikasi Hasil)
        result.Should().NotBeNull();
        result.Sku.Should().Be("SNK-CHIKI-001");

        // Verifikasi bahwa data stok (InventoryStock) bernilai 0 otomatis dibuat di kedua Outlet
        var stocks = await _dbContext.InventoryStocks.Where(s => s.ProductId == result.Id).ToListAsync();
        stocks.Should().HaveCount(2);
        stocks.All(s => s.QtyOnHand == 0).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_When_SkuDuplicate()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        _dbContext.Categories.Add(new Category { Id = categoryId, Name = "Snack" });
        _dbContext.Products.Add(new Product 
        { 
            Id = Guid.NewGuid(), 
            CategoryId = categoryId, 
            Sku = "SNK-DUPLICATE", 
            Name = "Existing", 
            Unit = "pcs" 
        });
        await _dbContext.SaveChangesAsync();

        var service = new ProductService(_dbContext, _currentUserMock);
        var request = new CreateProductRequest(
            CategoryId: categoryId,
            Sku: "SNK-DUPLICATE", // SKU yang sama
            Name: "New Product",
            Barcode: null,
            BasePrice: 10000,
            CostPrice: 8000,
            Unit: "pcs",
            IsConsignment: false
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
```

---

### B. Contoh Integration Test (`ProductsControllerTests.cs`)
Menguji API Endpoint `/api/products` secara riil untuk memastikan validasi Middleware dan token JWT berjalan dengan benar.

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MorrusPOS.Application.Features.Products;
using Xunit;

namespace MorrusPOS.IntegrationTests;

public class ProductsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProductsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_WithoutToken_Should_Return_401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

---

## 🚀 4. Cara Menjalankan Automated Testing

Setelah semua proyek terpasang dan kode test ditulis, Anda cukup menjalankan perintah berikut di root proyek MorrusPOS:

```bash
# Menjalankan seluruh test pada unit & integration projects
dotnet test
```

Perintah ini akan secara otomatis mencari semua proyek yang berekstensi `.UnitTests` dan `.IntegrationTests`, mengompilasi kode pengujian, menjalankan pengujian secara paralel, dan menampilkan laporan sukses/gagal di layar terminal Anda!
