# Panduan Manual Testing - Fase 4: Stok Opname & Transfer Cabang

Dokumen ini berisi panduan pengujian manual langkah-demi-langkah menggunakan Swagger UI (`/swagger`) untuk memverifikasi fitur Stok Opname dan Transfer Stok pada Fase 4.

---

## Prasyarat Pengujian
1. Pastikan backend berjalan (`dotnet run` atau `dotnet watch`).
2. Login terlebih dahulu melalui `/api/auth/login` menggunakan akun Admin atau Owner (yang memiliki permission `"stock.manage"`) untuk mendapatkan JWT Token, dan masukkan token tersebut ke tombol **Authorize** di Swagger UI.

---

## Skenario 1: Alur Stok Opname (Penyesuaian Fisik)

### Langkah 1: Cek Stok Produk Saat Ini
1. Catat `productId` salah satu produk dan panggil `/api/products/{id}`.
2. Catat jumlah stok saat ini di outlet Anda (contoh: `10` pcs).

### Langkah 2: Lakukan Stok Opname
1. Akses endpoint: `POST /api/stockopnames`
2. Kirim payload berikut untuk menyesuaikan jumlah fisik riil di laci/gudang (contoh: fisik dihitung ternyata ada `12` pcs, berarti selisih `+2` pcs):
   ```json
   {
     "outletId": "masukkan-outlet-id-anda",
     "items": [
       {
         "productId": "masukkan-product-id",
         "physicalQty": 12
       }
     ]
   }
   ```
3. **Respon Sukses (`201 Created`)**: Memuat data opname berstatus `"completed"` dengan `variance` bernilai `2`.
4. Panggil kembali `/api/products/{id}` dan pastikan stok produk tersebut sekarang telah ter-update secara riil menjadi `12`.

---

## Skenario 2: Alur Transfer Stok Antarcabang

### Langkah 1: Siapkan Dua Outlet (Asal & Tujuan)
1. Tentukan `outletId` asal (Cabang A, contoh stok barang: `15` pcs) dan `outletId` tujuan (Cabang B, contoh stok barang: `0` pcs).

### Langkah 2: Ajukan Transfer Stok (Pending)
1. Akses endpoint: `POST /api/stocktransfers`
2. Kirim payload berikut untuk memindahkan `5` pcs barang dari Cabang A ke Cabang B:
   ```json
   {
     "fromOutletId": "id-cabang-a",
     "toOutletId": "id-cabang-b",
     "items": [
       {
         "productId": "id-produk",
         "qty": 5
       }
     ]
   }
   ```
3. **Respon Sukses (`201 Created`)**:
   - Memuat data transfer berstatus `"pending"`.
   - Pastikan Anda mencatat `id` transaksi transfer tersebut.
4. **Verifikasi Awal**: Cek stok barang di Cabang A dan Cabang B. Stok Cabang A harus tetap `15` dan Cabang B tetap `0` (karena status masih `pending`).

### Langkah 3: Tolak Transfer Stok (Rejection Test)
1. Buat transaksi transfer stok baru seperti langkah 2 dan catat ID-nya.
2. Panggil endpoint `POST /api/stocktransfers/{id}/reject`.
3. **Respon Sukses (`200 OK`)**: Status transfer berubah menjadi `"rejected"`.
4. Cek stok kedua outlet. Stok tidak boleh mengalami perubahan.

### Langkah 4: Setujui Transfer Stok dengan Stok Kurang (Safety Test)
1. Buat transaksi transfer stok baru seperti langkah 2, namun masukkan `qty` melebihi stok Cabang A (contoh: minta `50` pcs sedangkan stok hanya `15`). Catat ID transfernya.
2. Panggil endpoint `POST /api/stocktransfers/{id}/approve`.
3. **Respon Error (`400 Bad Request` / `500 Internal Error`)**: Sistem harus menolak persetujuan dengan pesan error: *"Stok tidak mencukupi di outlet asal..."*. Stok Cabang A tetap `15`.

### Langkah 5: Setujui Transfer Stok Sukses (Approval Test)
1. Panggil kembali endpoint `POST /api/stocktransfers/{id}/approve` untuk ID transfer yang diajukan di **Langkah 2** (meminta `5` pcs).
2. **Respon Sukses (`200 OK`)**: Status transfer berubah menjadi `"approved"`.
3. **Verifikasi Akhir**:
   - Stok Cabang A terpotong secara riil menjadi `10` pcs (`15 - 5`).
   - Stok Cabang B bertambah secara riil menjadi `5` pcs (`0 + 5`).
   - Log mutasi persediaan secara otomatis tercatat di `StockLedger` (tipe: `transfer_out` di Cabang A dan `transfer_in` di Cabang B).
