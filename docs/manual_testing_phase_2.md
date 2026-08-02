# Panduan Manual Testing - Fase 2: Master Produk & Kategori (CRUD & Log Audit)

Dokumen ini berisi panduan langkah-demi-langkah pengujian manual fitur **Kategori, CRUD Produk, Seeding Stok & Audit Trail Harga (Fase 2)** pada API MorrusPOS menggunakan **Swagger UI**.

---

## 🔐 Persiapan Awal (Login & Otorisasi)

1. Pastikan Anda telah melakukan otorisasi di Swagger menggunakan token akun **Owner** (`owner@morruspos.com` / `owner123`) seperti dijelaskan pada Panduan Testing Fase 1.
2. Karena endpoint pengubahan produk (`POST`, `PUT`, `DELETE`) dilindungi oleh permission `product.manage`, maka pengguna wajib memiliki role **Owner** atau **Gudang/Admin** yang memiliki permission tersebut.

---

## 📁 Skenario 1: CRUD Kategori

Tujuan: Menguji pembuatan kategori induk dan sub-kategori serta pencegahan circular reference.

### A. Membuat Kategori Induk
1. Buka kelompok **Categories** $\rightarrow$ **`POST /api/Categories`**.
2. Klik **Try it out**, ubah request body menjadi:
   ```json
   {
     "name": "Makanan",
     "parentId": null
   }
   ```
3. Klik **Execute**.
4. **Hasil yang diharapkan**:
   * Respon: **`201 Created`**.
   * Salin `id` kategori "Makanan" yang baru dibuat tersebut.

### B. Membuat Sub-Kategori (Induk Valid)
1. Buka kelompok **Categories** $\rightarrow$ **`POST /api/Categories`** lagi.
2. Isi request body (ganti `parentId` dengan ID kategori "Makanan"):
   ```json
   {
     "name": "Snack",
     "parentId": "MASUKKAN-ID-KATEGORI-MAKANAN-DI-SINI"
   }
   ```
3. Klik **Execute**.
4. **Hasil yang diharapkan**: Respon **`201 Created`**.

---

## 📦 Skenario 2: Pembuatan Produk & Auto-Seeding Stok

Tujuan: Membuat produk baru dan memverifikasi bahwa stok awal sebesar `0` dibuat secara otomatis untuk seluruh outlet yang ada.

1. Buka kelompok **Products** $\rightarrow$ **`POST /api/Products`**.
2. Klik **Try it out**, lalu masukkan payload berikut (ganti `categoryId` dengan ID kategori "Snack" or "Makanan"):
   ```json
   {
     "categoryId": "MASUKKAN-ID-KATEGORI-SNACK-DI-SINI",
     "sku": "SNK-CHIKI-001",
     "name": "Chiki Balls Keju 50g",
     "barcode": "8991234567890",
     "basePrice": 12000,
     "costPrice": 9500,
     "unit": "pcs",
     "isConsignment": false
   }
   ```
3. Klik **Execute**.
4. **Hasil yang diharapkan**:
   * Respon: **`201 Created`** dengan data produk lengkap.
5. **Verifikasi Stok Otomatis**:
   * Buka kelompok **Products** $\rightarrow$ **`GET /api/Products`**.
   * Isi parameter `outletId` (misal Outlet Utama: `8bba5427-017e-40fb-886f-5e4c6c9a3809`).
   * Klik **Execute**.
   * **Hasil yang diharapkan**: Produk "Chiki Balls Keju 50g" akan muncul di dalam daftar dengan nilai **`qtyOnHand: 0`**. Ini membuktikan sistem berhasil men-seed stok awal secara otomatis.

---

## 📈 Skenario 3: Audit Trail Perubahan Harga

Tujuan: Menguji pencatatan histori perubahan harga jual (`BasePrice`) dan harga modal (`CostPrice`) ke tabel `audit_logs` secara otomatis.

1. Buka kelompok **Products** $\rightarrow$ **`PUT /api/Products/{id}`**.
2. Masukkan parameter `id` produk "Chiki Balls Keju 50g" yang baru dibuat.
3. Kirim payload perubahan harga (misal menaikkan `basePrice` menjadi `13000` dan `costPrice` menjadi `10000`):
   ```json
   {
     "categoryId": "MASUKKAN-ID-KATEGORI-SNACK-DI-SINI",
     "sku": "SNK-CHIKI-001",
     "name": "Chiki Balls Keju 50g (Harga Baru)",
     "barcode": "8991234567890",
     "basePrice": 13000,
     "costPrice": 10000,
     "unit": "pcs",
     "isConsignment": false,
     "isActive": true
   }
   ```
4. Klik **Execute**.
5. **Hasil yang diharapkan**: Respon **`200 OK`**.
6. **Verifikasi di Database (Audit Log)**:
   * Query tabel `audit_logs` di database Anda.
   * **Hasil yang diharapkan**: Muncul log audit baru dengan `action = "price_change"`, `oldValueJson = {"BasePrice":12000.00,"CostPrice":9500.00}`, dan `newValueJson = {"BasePrice":13000.00,"CostPrice":10000.00}`.

---

## 🛡️ Skenario 4: Pengujian Safe Delete (Soft Delete untuk Produk yang Pernah Terjual)

Tujuan: Memastikan produk yang memiliki riwayat penjualan di tabel `transaction_items` tidak dihapus keras secara permanen dari database melainkan hanya dinonaktifkan (`IsActive = false`).

1. Buka kelompok **Products** $\rightarrow$ **`DELETE /api/Products/{id}`**.
2. Masukkan parameter `id` produk yang dicoba untuk dihapus.
3. Klik **Execute**.
4. **Hasil yang diharapkan**:
   * Jika produk **belum pernah** dijual (baru dibuat): Produk akan terhapus keras dari tabel `products` beserta baris stok di `inventory_stocks` (respon **`204 NoContent`**).
   * Jika produk **sudah pernah** terjual (misal ada record transaksi di database): Produk tidak dihapus dari tabel, namun kolom `IsActive` diubah menjadi `false` (tidak aktif), sehingga data penjualan historis di laporan keuangan masa lalu tetap aman.
