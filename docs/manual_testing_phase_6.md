# Panduan Manual Testing - Fase 6: Barang Titipan / Konsinyasi (Bagi Hasil Otomatis)

Dokumen ini berisi panduan pengujian manual langkah-demi-langkah menggunakan Swagger UI (`/swagger`) untuk memverifikasi fitur Tanda Terima Konsinyasi, Hook Transaksi Checkout, dan Settlement Hak Supplier.

---

## Prasyarat Pengujian
1. Pastikan backend berjalan (`dotnet watch` atau `dotnet run` di folder `MorrusPOS.Api`).
2. Login terlebih dahulu melalui `/api/auth/login` menggunakan akun Owner atau Admin (yang memiliki permission `"consignment.manage"` dan `"supplier.manage"`) untuk mendapatkan JWT Token, dan masukkan token tersebut ke tombol **Authorize** di Swagger UI.
3. Siapkan `outletId` (Cabang) dan `supplierId` (Supplier aktif). Jika belum ada supplier, buat terlebih dahulu via `POST /api/suppliers`.

---

## Skenario 1: Tanda Terima Konsinyasi (Draft & Received)

### Langkah 1: Buat Produk Master Biasa
1. Buat produk baru via `POST /api/products` (contoh: Sku `"SKU-KONSIN"`, Name `"Kripik Tempe Titipan"`, `isConsignment: false` - biarkan false untuk menguji auto-upgrade saat tanda terima selesai).
2. Catat `productId` yang dihasilkan.

### Langkah 2: Buat Tanda Terima Konsinyasi (Draft)
1. Akses endpoint: `POST /api/consignments`
2. Kirim payload berikut (Menerima 50 unit dengan bagi hasil/unitCost Rp 3.000, harga jual/unitPrice Rp 5.000):
   ```json
   {
     "supplierId": "id-supplier-anda",
     "outletId": "id-outlet-anda",
     "items": [
       {
         "productId": "id-produk-di-langkah-1",
         "qty": 50,
         "unitCost": 3000,
         "unitPrice": 5000
       }
     ]
   }
   ```
3. **Respon Sukses (`201 Created`)**:
   - `status` bernilai `"draft"`.
   - `consignmentNumber` berformat `"CSG-yyyyMMddHHmmss-XXXX"`.
4. Catat `id` konsinyasi (disebut `{CONSIGNMENT_ID}`).

### Langkah 3: Selesaikan Tanda Terima Konsinyasi (Received)
1. Cek stok produk saat ini melalui `GET /api/products/{productId}` (stok harus bernilai `0`).
2. Akses endpoint: `PUT /api/consignments/{CONSIGNMENT_ID}/status`
3. Kirim payload:
   ```json
   {
     "status": "received"
   }
   ```
4. **Respon Sukses (`200 OK`)**: `status` berubah menjadi `"received"`.
5. **Verifikasi Otomatis**:
   - Panggil `GET /api/products/{productId}`.
   - Stok barang harus bertambah menjadi `50`.
   - Bendera `isConsignment` pada produk otomatis berubah menjadi `true`.
   - `costPrice` (HPP) produk otomatis ter-update menjadi `3000`.

---

## Skenario 2: Hook Transaksi POS Checkout (Bagi Hasil Otomatis)

### Langkah 1: Lakukan Checkout POS Kasir
1. Buka sesi kasir jika belum terbuka via `POST /api/cashiersessions`. Catat `cashierSessionId`-nya.
2. Panggil endpoint `POST /api/transactions` (Checkout POS) untuk menjual produk konsinyasi yang baru diterima tadi sebanyak `2` pcs:
   ```json
   {
     "id": "generate-guid-baru-di-sini",
     "outletId": "id-outlet-anda",
     "cashierSessionId": "id-sesi-kasir-aktif",
     "channel": "walk_in",
     "subtotal": 10000,
     "discountTotal": 0,
     "taxTotal": 0,
     "grandTotal": 10000,
     "items": [
       {
         "productId": "id-produk-konsinyasi",
         "qty": 2,
         "unitPrice": 5000,
         "discountAmount": 0
       }
     ],
     "payments": [
       {
         "method": "cash",
         "amount": 10000,
         "referenceNumber": null
       }
     ]
   }
   ```
3. **Respon Sukses (`200 OK`)**.

### Langkah 2: Verifikasi Penjualan Konsinyasi Terbentuk Otomatis
1. Panggil endpoint: `GET /api/consignmentsettlements/unpaid-sales/{supplierId}`
2. **Respon Sukses (`200 OK`)**: Harus muncul data penjualan konsinyasi berstatus `"unpaid"` untuk supplier tersebut:
   - `qty: 2`
   - `unitCost: 3000`
   - `totalAmount: 6000` (2 * 3000)
   - `status: "unpaid"`

---

## Skenario 3: Settlement Konsinyasi (Draft, Settled, & Cancelled)

### Langkah 1: Buat Settlement Baru (Draft)
1. Akses endpoint: `POST /api/consignmentsettlements`
2. Kirim payload:
   ```json
   {
     "supplierId": "id-supplier-anda"
   }
   ```
3. **Respon Sukses (`201 Created`)**:
   - `status` bernilai `"draft"`.
   - `totalAmount` bernilai `6000` (menghimpun penjualan unpaid dari skenario 2).
   - `sales` memuat daftar penjualan konsinyasi yang terikat.
4. Catat `id` settlement (disebut `{SETTLEMENT_ID}`).

### Langkah 2: Uji Pembatalan Settlement (Cancelled)
1. Akses endpoint: `PUT /api/consignmentsettlements/{SETTLEMENT_ID}/status`
2. Kirim payload:
   ```json
   {
     "status": "cancelled"
   }
   ```
3. **Respon Sukses (`200 OK`)**: Status settlement berubah menjadi `"cancelled"`.
4. **Verifikasi Lepas Penjualan**: Panggil kembali `GET /api/consignmentsettlements/unpaid-sales/{supplierId}`. Dua penjualan konsinyasi tadi harus muncul kembali sebagai `"unpaid"` (lepas ikatan dari settlement yang dibatalkan).

### Langkah 3: Buat Kembali Settlement Baru & Setujui (Settled/Lunas)
1. Buat kembali settlement baru seperti Langkah 1. Catat `id` yang baru (disebut `{NEW_SETTLEMENT_ID}`).
2. Akses endpoint: `PUT /api/consignmentsettlements/{NEW_SETTLEMENT_ID}/status`
3. Kirim payload:
   ```json
   {
     "status": "settled"
   }
   ```
4. **Respon Sukses (`200 OK`)**: Status settlement menjadi `"settled"`.
5. **Verifikasi Akhir Lunas**:
   - Panggil `GET /api/consignmentsettlements/unpaid-sales/{supplierId}` -> harus mengembalikan daftar kosong (karena semua sudah lunas terbayar).
   - Panggil `GET /api/consignmentsettlements/{NEW_SETTLEMENT_ID}` -> status settlement `"settled"` dan status semua penjualan di dalamnya harus berubah menjadi `"paid"`.
