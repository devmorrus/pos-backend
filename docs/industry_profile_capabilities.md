# Profil Industri & Capability

Dokumen ini menjelaskan cara MorrusPOS menentukan fitur aktif untuk sebuah bisnis dan outlet, serta aturan perubahan konfigurasinya.

## Profil siap pakai

| Kode | Nama tampilan | Capability aktif |
| --- | --- | --- |
| `general` | General | 7 |
| `electronics` | Elektronik | 16 |
| `food-beverage` | F&B | 18 |
| `retail` | Retail Umum | 16 |
| `grocery` | Grocery | 17 |
| `fashion` | Fashion | 14 |
| `services` | Services | 8 |

Kode profil bersifat stabil dan dipakai API. Perubahan nama tampilan tidak mengubah kode.

Profil **Elektronik** mengikuti matriks Retail Umum:

- Aktif: `sales.pos`, `sales.transactions`, `customers`, `catalog.products`, `catalog.variants`, `inventory.stock`, `inventory.opname`, `inventory.transfer`, `procurement.purchasing`, `procurement.supplier_debt`, `procurement.supplier_return`, `channels.settlement`, `pricing.promotions`, `pricing.vouchers`, `reports.operational`, `storefront.public`.
- Tidak aktif: `catalog.modifiers`, `catalog.recipes`, `catalog.batch_expiry`, `inventory.buffer_stock`, `consignment`, `channels.gobiz`, `pricing.tax_service`, `accounting.core`, `accounting.cashflow`, `accounting.integration`, `reports.accounting`.

Serial/IMEI dan garansi belum menjadi capability tersendiri.

## Urutan evaluasi

Untuk setiap capability, nilai efektif dihitung dengan urutan:

1. `Capability.DefaultEnabled` (default global).
2. Mapping profil industri bisnis.
3. Override Business.
4. Override Outlet, hanya untuk capability ber-scope `Outlet`.

Aturan penting:

- Override Business selalu menang atas profil.
- Override Outlet hanya dapat mematikan capability, bukan mengaktifkan capability yang dimatikan Business. Jika Business mematikan capability, outlet tidak dapat mengaktifkannya kembali.
- Capability yang tidak dikenal atau tidak ada di katalog dianggap **nonaktif** (*fail closed*).
- Perubahan profil hanya mengubah mapping capability dan revision. Data bisnis, transaksi, stok, dan override yang sudah ada tidak dihapus.

Contoh:

```text
Profil Retail Umum: consignment = false
Business override:  consignment = true
Outlet override:    consignment = false
Hasil di outlet:    false (sumber: OutletOverride)
```

```text
Profil Retail Umum: inventory.buffer_stock = true
Business override:  inventory.buffer_stock = false
Outlet override:    inventory.buffer_stock = true
Hasil di outlet:    false (sumber: BusinessDisabled)
```

## Hak akses

- **Owner** dapat mengganti profil industri, override Business, dan override Outlet.
- **Admin** hanya dapat membaca konfigurasi.
- Endpoint baca memakai permission `capability.view`; endpoint perubahan memakai `capability.manage` dan tetap divalidasi role Owner di service.

## Kontrak error

- Capability nonaktif: HTTP `403` dengan kode `CAPABILITY_DISABLED`.
- Revision tidak cocok: HTTP `409` dengan kode `CAPABILITY_REVISION_CONFLICT`. Klien harus memuat ulang snapshot lalu mengulang perubahan.
- Setiap perubahan menaikkan `CapabilityRevision` dan menulis audit ke `capability_change_logs`.

## Onboarding

Daftar profil diambil dari `GET /api/public/industry-profiles`, bukan hardcode di frontend. Registrasi mengirim `business.industryProfileCode`; backend memetakan kategori bisnis dari profil tersebut. Jika profil tidak valid, registrasi ditolak.

## Migrasi dan backfill

Migration `20261103025159_AddElectronicsIndustryProfile`:

- Menambah profil `electronics` beserta 27 mapping capability.
- Mengubah nama tampilan menjadi `F&B` dan `Retail Umum` serta mengatur urutan profil.
- Mem-backfill bisnis yang masih memakai profil `general` dengan kategori `electronics`, `electronic`, atau `elektronik` (tanpa membedakan huruf besar/kecil dan spasi tepi).
- Hanya memproses bisnis yang memiliki `OwnerId` valid pada tenant yang sama; setiap backfill menaikkan revision dan menulis audit `IndustryProfileBackfilled`.
- Tidak mengubah bisnis yang sudah memakai profil lain dan tidak menyentuh override Business/Outlet.

`Down` migration sengaja tidak didukung karena rollback dapat memutus konfigurasi tenant. Rollback aplikasi aman dilakukan tanpa menghapus data profil. Jika perlu kembali ke profil lama, lakukan lewat halaman **Pengaturan > Profil Industri & Fitur** agar revision dan audit tetap tercatat.

## Verifikasi

Setelah deploy, jalankan:

```bash
psql -U morrus -d morrusposs -v ON_ERROR_STOP=1 -f db/tenant-verify.sql
```

Script memeriksa katalog capability/profil, kelengkapan mapping, duplikasi kode profil, RLS, kepemilikan tabel, dan hak akses role runtime.

Pengujian terkait:

```bash
dotnet test tests/MorrusPOS.UnitTests/MorrusPOS.UnitTests.csproj
dotnet test tests/MorrusPOS.IntegrationTests/MorrusPOS.IntegrationTests.csproj
cd ../frontend && npm run test:e2e
```
