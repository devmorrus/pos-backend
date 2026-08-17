# MorrusPOS Backend

Backend POS berbasis ASP.NET Core dengan pola Clean Architecture.

README ini ditulis untuk orang yang baru clone project dari GitHub dan ingin menjalankannya dari nol sampai API hidup dengan `dotnet run`.

## Stack

- .NET SDK 10
- ASP.NET Core Web API
- Entity Framework Core 10
- PostgreSQL
- Swagger / OpenAPI

## Struktur Project

```text
MorrusPOS/
├── MorrusPOS.sln
└── src/
    ├── MorrusPOS.Domain/
    ├── MorrusPOS.Application/
    ├── MorrusPOS.Infrastructure/
    └── MorrusPOS.Api/
```

Aturan dependency:

`Api -> Infrastructure -> Application -> Domain`

## Prasyarat

Pastikan ini sudah terpasang di komputer:

1. `.NET SDK 10`
2. `PostgreSQL`
3. `psql` command line client

Cek cepat:

```bash
dotnet --version
psql --version
```

## 1. Clone dan buka project

Clone repository dari GitHub:

```bash
git clone <URL_REPOSITORY_GITHUB>
```

Masuk ke folder project:

```bash
cd MorrusPOS
```

Opsional, cek isi folder:

```bash
ls
```

Yang seharusnya ada minimal:

- `MorrusPOS.sln`
- `src/`
- `README.md`

## 2. Restore package dan build

Jalankan dari root project:

```bash
dotnet restore
dotnet build
```

Kalau `dotnet build` berhasil, berarti source code siap lanjut ke setup database.

## 3. Siapkan PostgreSQL

Project ini butuh database PostgreSQL.

Kamu bisa pakai:

- PostgreSQL lokal yang sudah terpasang di komputer
- PostgreSQL lewat Docker

### Opsi A: PostgreSQL lokal

Pastikan kamu punya:

- hostname: `localhost`
- port: `5432`
- username database
- password database

Kalau database belum ada, buat dulu:

```bash
createdb -h localhost -U YOUR_DB_USER morruspos
```

Kalau kamu ingin nama database lain, boleh. Nanti samakan di connection string.

### Opsi B: PostgreSQL via Docker

```bash
docker run --name morruspos-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=morruspos \
  -p 5432:5432 \
  -d postgres:latest
```

Kalau pakai Docker di atas, connection string-nya tinggal menyesuaikan dengan:

- database: `morruspos`
- username: `postgres`
- password: `postgres`

## 4. Atur `appsettings.json`

Edit file [src/MorrusPOS.Api/appsettings.json](/Users/hilmi/Downloads/MorrusPOS/src/MorrusPOS.Api/appsettings.json:1).

Contoh isi yang aman untuk development:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=morruspos;Username=YOUR_DB_USER;Password=YOUR_DB_PASSWORD"
  },
  "Jwt": {
    "Secret": "GANTI_DENGAN_SECRET_MINIMAL_32_KARAKTER",
    "Issuer": "MorrusPOS",
    "Audience": "MorrusPOS.Client",
    "AccessTokenMinutes": "60"
  },
  "Frontend": {
    "Url": "http://localhost:5173"
  }
}
```

Catatan:

- `Jwt:Secret` wajib panjang dan jangan pakai contoh default di production.
- Nama database di connection string harus benar-benar sama dengan database yang kamu buat.
- Kalau kamu pakai database bernama `morrusposs`, maka connection string juga harus memakai `morrusposs`.

## 5. Buat migration awal

Masuk ke project API:

```bash
cd src/MorrusPOS.Api
```

Buat migration:

```bash
dotnet ef migrations add InitialCreate --project ../MorrusPOS.Infrastructure
```

Kalau migration sudah pernah ada di repo, command ini bisa dilewati. Di repo ini migration awal biasanya sudah tersedia di:

- `src/MorrusPOS.Infrastructure/Migrations/`

## 6. Apply migration ke database

Masih dari folder `src/MorrusPOS.Api`, jalankan:

```bash
dotnet ef database update --project ../MorrusPOS.Infrastructure
```

Kalau berhasil, semua tabel utama akan dibuat di PostgreSQL.

## 7. Pasang trigger stok

Project ini memakai trigger PostgreSQL untuk mengupdate `inventory_stock.qty_on_hand` secara otomatis dari `stock_ledger`.

Jalankan:

```bash
psql -h localhost -U YOUR_DB_USER -d morruspos -f ../MorrusPOS.Infrastructure/Persistence/Migrations/StockLedgerTrigger.sql
```

Kalau nama database kamu bukan `morruspos`, ganti sesuai nama database yang dipakai di connection string.

Contoh:

```bash
psql -h localhost -U irok -d morrusposs -f ../MorrusPOS.Infrastructure/Persistence/Migrations/StockLedgerTrigger.sql
```

## 8. Jalankan API

Masih dari folder `src/MorrusPOS.Api`:

```bash
dotnet run
```

Atau dari root project:

```bash
dotnet run --project src/MorrusPOS.Api
```

Kalau sukses, terminal akan menampilkan URL aplikasi. Biasanya Swagger bisa dibuka di:

```text
https://localhost:7100/swagger
```

Kalau port berbeda, ikuti URL yang muncul di terminal.

## 9. Test cepat

Setelah API hidup:

1. Buka Swagger.
2. Coba endpoint auth yang tersedia.
3. Coba endpoint product yang tersedia.

Controller yang saat ini ada:

- `AuthController`
- `ProductsController`

## Alur Setup Singkat

Kalau kamu cuma butuh urutan super singkat:

```bash
git clone <URL_REPOSITORY_GITHUB>
cd MorrusPOS
dotnet restore
dotnet build

# edit src/MorrusPOS.Api/appsettings.json

cd src/MorrusPOS.Api
dotnet ef database update --project ../MorrusPOS.Infrastructure
psql -h localhost -U YOUR_DB_USER -d morruspos -f ../MorrusPOS.Infrastructure/Persistence/Migrations/StockLedgerTrigger.sql
dotnet run
```

## Troubleshooting

### `Couldn't find a project to run`

Penyebab:

- Menjalankan `dotnet run` dari root repo

Solusi:

```bash
dotnet run --project src/MorrusPOS.Api
```

atau:

```bash
cd src/MorrusPOS.Api
dotnet run
```

### `role "postgres" does not exist`

Penyebab:

- Connection string memakai user `postgres`, tapi di PostgreSQL lokal user itu tidak ada

Solusi:

- Ganti `Username` dan `Password` di `appsettings.json` ke user database yang benar
- Atau buat role `postgres` di PostgreSQL

### `database "<nama_db>" does not exist`

Solusi:

```bash
createdb -h localhost -U YOUR_DB_USER YOUR_DB_NAME
```

Lalu ulangi:

```bash
dotnet ef database update --project ../MorrusPOS.Infrastructure
```

### Error saat membuat index `Barcode`

Masalah ini sudah diperbaiki di repo ini. Kalau error serupa muncul lagi, pastikan kamu memakai source terbaru yang berisi filter PostgreSQL:

```text
"Barcode" IS NOT NULL
```

### `Your startup project doesn't reference Microsoft.EntityFrameworkCore.Design`

Masalah ini juga sudah diperbaiki di repo ini. Kalau masih muncul, jalankan:

```bash
dotnet restore
dotnet build
```

Lalu ulangi command EF.

## Fitur Utama & Modul Bisnis

Sistem backend ini mengimplementasikan modul bisnis retail POS lengkap:

1. **Autentikasi & Multi-tenant**:
   - JWT authentication dengan role-based authorization.
   - Tenant isolation berbasis outlet secara otomatis untuk mencegah kasir mengakses transaksi cabang lain.

2. **Katalog & Manajemen Stok**:
   - Produk, varian produk, dan kategori.
   - Pergerakan stok terintegrasi ke `StockLedger`.
   - Update otomatis kuantitas stok produk (`QtyOnHand`) via PostgreSQL DB Trigger.

4. **Sesi Kasir (Shift Control) & Rekonsiliasi**:
   - Buka dan tutup shift kasir secara mandiri dengan verifikasi kas awal (`OpeningCash`).
   - Rekonsiliasi kas otomatis di laci kas saat tutup sesi kasir (`ExpectedCash = OpeningCash + CashSales - PettyCashExpenses`).
   - Perhitungan otomatis selisih kas aktual vs kas tercatat (*variance analysis*).
   - Pengelompokkan summary penerimaan non-tunai (QRIS, EDC, Transfer) untuk pembukuan harian.

5. **Kas Kecil (Petty Cash Expenses)**:
   - Pencatatan pengeluaran operasional harian (seperti ATK, konsumsi, transportasi) langsung memotong saldo kas laci POS aktif.
   - Validasi ketat: Kasir wajib memiliki sesi kasir aktif yang masih terbuka untuk dapat melakukan pencatatan kas keluar.

## Saran Setelah API Berjalan

1. Buat data awal: outlet, role, permission, dan user.
2. Uji login lewat Swagger.
3. Tambahkan seeding untuk environment development.
4. Lanjut implementasi service yang belum lengkap (seperti Konsinyasi atau Supplier PO).

## File Penting

- [README.md](/Users/hilmi/Downloads/MorrusPOS/README.md:1)
- [src/MorrusPOS.Api/Program.cs](/Users/hilmi/Downloads/MorrusPOS/src/MorrusPOS.Api/Program.cs:1)
- [src/MorrusPOS.Api/appsettings.json](/Users/hilmi/Downloads/MorrusPOS/src/MorrusPOS.Api/appsettings.json:1)
- [src/MorrusPOS.Infrastructure/Persistence/AppDbContext.cs](/Users/hilmi/Downloads/MorrusPOS/src/MorrusPOS.Infrastructure/Persistence/AppDbContext.cs:1)
- [src/MorrusPOS.Infrastructure/Persistence/Migrations/StockLedgerTrigger.sql](/Users/hilmi/Downloads/MorrusPOS/src/MorrusPOS.Infrastructure/Persistence/Migrations/StockLedgerTrigger.sql:1)
