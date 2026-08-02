# Panduan Manual Testing - Fase 5: Supplier, Pembelian (PO) & Utang Usaha

Dokumen ini berisi panduan pengujian manual langkah-demi-langkah menggunakan Swagger UI (`/swagger`) untuk memverifikasi seluruh fitur Fase 5: Master Supplier, Purchase Order (PO), dan Buku Utang Usaha.

---

## Prasyarat Pengujian

1. Pastikan backend berjalan (`dotnet watch` atau `dotnet run` di folder `MorrusPOS.Api`).
2. Buka browser dan akses **Swagger UI**: `https://localhost:{PORT}/swagger`
3. Login melalui `POST /api/auth/login` menggunakan akun Owner atau Admin (yang memiliki permission `"supplier.manage"`):
   ```json
   {
     "email": "owner@morruspos.com",
     "password": "owner123"
   }
   ```
4. Salin nilai `accessToken` dari respon, lalu klik tombol **Authorize** di pojok kanan atas Swagger dan tempelkan token tersebut.

---

## Bagian A: Master Data Supplier

### A1. Buat Supplier Baru
1. Akses endpoint: `POST /api/suppliers`
2. Kirim payload berikut:
   ```json
   {
     "name": "PT Berkah Jaya Abadi",
     "contactPerson": "Budi Santoso",
     "phone": "08123456789",
     "email": "budi@berkah.co.id",
     "address": "Jl. Industri No. 12, Jakarta"
   }
   ```
3. **Respon Sukses (`201 Created`)**: Data supplier baru muncul dengan `id` yang dihasilkan sistem dan `isActive: true`.
4. **Catat `id` supplier** untuk digunakan di langkah berikutnya (contoh disebut `{SUPPLIER_ID}`).

### A2. Lihat Semua Supplier Aktif
1. Akses endpoint: `GET /api/suppliers`
2. **Respon Sukses (`200 OK`)**: Daftar semua supplier aktif terlihat, termasuk supplier yang baru dibuat.

### A3. Perbarui Supplier (Nonaktifkan)
1. Akses endpoint: `PUT /api/suppliers/{SUPPLIER_ID}`
2. Kirim payload dengan mengubah `isActive` menjadi `false` (simulasi soft-delete supplier):
   ```json
   {
     "name": "PT Berkah Jaya Abadi",
     "contactPerson": "Budi Santoso",
     "phone": "08123456789",
     "email": "budi@berkah.co.id",
     "address": "Jl. Industri No. 12, Jakarta",
     "isActive": false
   }
   ```
3. **Respon Sukses (`200 OK`)**: `isActive` berubah menjadi `false`.
4. Panggil kembali `GET /api/suppliers` — supplier tersebut tidak muncul lagi dalam daftar aktif.
5. **Pulihkan kembali** dengan memanggil `PUT /api/suppliers/{SUPPLIER_ID}` dan set `isActive: true` agar bisa digunakan di langkah selanjutnya.

---

## Bagian B: Purchase Order — Pembelian Tunai (Cash)

### B1. Buat PO Cash (Status Draft)

> **Catatan**: Anda perlu `productId` dari salah satu produk yang sudah ada. Cari produk via `GET /api/products` dan catat `id` salah satu produk (disebut `{PRODUCT_ID}`) serta `outletId` Anda (disebut `{OUTLET_ID}`).

1. Akses endpoint: `POST /api/purchaseorders`
2. Kirim payload berikut (PO tunai, 50 unit produk dengan harga beli Rp 3.500/unit):
   ```json
   {
     "supplierId": "{SUPPLIER_ID}",
     "outletId": "{OUTLET_ID}",
     "paymentType": "cash",
     "dueDate": null,
     "items": [
       {
         "productId": "{PRODUCT_ID}",
         "qty": 50,
         "unitCost": 3500
       }
     ]
   }
   ```
3. **Respon Sukses (`201 Created`)**:
   - `status` harus bernilai `"draft"`.
   - `poNumber` harus berformat `"PO-yyyyMMddHHmmss-XXXX"`.
   - `totalAmount` harus bernilai `175000` (50 × 3500).
4. **Catat `id` PO** (disebut `{PO_CASH_ID}`).

### B2. Ubah Status PO Cash ke Pending
1. Akses endpoint: `PUT /api/purchaseorders/{PO_CASH_ID}/status`
2. Kirim payload:
   ```json
   { "status": "pending" }
   ```
3. **Respon Sukses (`200 OK`)**: Status PO berubah menjadi `"pending"`.

### B3. Selesaikan PO Cash (Barang Diterima → Completed)
1. Catat stok produk saat ini (`QtyOnHand`) via `GET /api/products/{PRODUCT_ID}`.
2. Akses endpoint: `PUT /api/purchaseorders/{PO_CASH_ID}/status`
3. Kirim payload:
   ```json
   { "status": "completed" }
   ```
4. **Respon Sukses (`200 OK`)**: Status PO berubah menjadi `"completed"`.
5. **Verifikasi Stok**: Panggil kembali `GET /api/products/{PRODUCT_ID}` dan pastikan `QtyOnHand` bertambah **50 unit**.
6. **Verifikasi HPP**: Pastikan `costPrice` produk berubah menjadi **3500** (diperbarui dari nilai lama).
7. **Verifikasi Utang**: Panggil `GET /api/supplierdebts` dan pastikan **tidak ada** entri utang untuk PO Cash ini (hanya PO Tempo yang menciptakan utang).

### B4. Uji Penolakan Transisi Ganda (Double Completion Guard)
1. Coba ubah status PO yang sudah `completed` lagi:
   ```json
   { "status": "completed" }
   ```
2. **Respon Error (`400`/`500`)**: Sistem harus menolak dengan pesan *"sudah berstatus 'completed', tidak bisa diubah kembali"*.

---

## Bagian C: Purchase Order — Pembelian Tempo (Kredit)

### C1. Uji Validasi Tempo Tanpa DueDate
1. Akses endpoint: `POST /api/purchaseorders`
2. Kirim payload dengan `paymentType: "tempo"` namun `dueDate: null`:
   ```json
   {
     "supplierId": "{SUPPLIER_ID}",
     "outletId": "{OUTLET_ID}",
     "paymentType": "tempo",
     "dueDate": null,
     "items": [
       { "productId": "{PRODUCT_ID}", "qty": 10, "unitCost": 5000 }
     ]
   }
   ```
3. **Respon Error**: Sistem harus menolak dengan pesan *"DueDate wajib diisi"*.

### C2. Buat PO Tempo dengan DueDate
1. Akses endpoint: `POST /api/purchaseorders`
2. Kirim payload PO Tempo yang valid (jatuh tempo 30 hari ke depan):
   ```json
   {
     "supplierId": "{SUPPLIER_ID}",
     "outletId": "{OUTLET_ID}",
     "paymentType": "tempo",
     "dueDate": "2026-09-02T00:00:00Z",
     "items": [
       { "productId": "{PRODUCT_ID}", "qty": 30, "unitCost": 5000 }
     ]
   }
   ```
3. **Respon Sukses (`201 Created`)**: `status: "draft"`, `totalAmount: 150000` (30 × 5000).
4. **Catat `id` PO Tempo** (disebut `{PO_TEMPO_ID}`).

### C3. Selesaikan PO Tempo → Utang Otomatis Terbentuk
1. Ubah status PO Tempo menjadi `completed`:
   ```json
   { "status": "completed" }
   ```
   (`PUT /api/purchaseorders/{PO_TEMPO_ID}/status`)
2. **Respon Sukses (`200 OK`)**: Status PO berubah menjadi `"completed"`.
3. **Verifikasi Stok**: Produk harus bertambah **30 unit**.
4. **Verifikasi HPP**: `costPrice` produk harus berubah menjadi **5000**.
5. **Verifikasi Utang Terbentuk Otomatis**: Panggil `GET /api/supplierdebts` — harus muncul tagihan baru:
   - `amount: 150000`
   - `paidAmount: 0`
   - `remainingAmount: 150000`
   - `status: "unpaid"`
   - `dueDate` sesuai yang diinput.

---

## Bagian D: Buku Utang Usaha — Pembayaran

### D1. Lihat Semua Utang Belum Lunas
1. Akses endpoint: `GET /api/supplierdebts?status=unpaid`
2. **Respon Sukses (`200 OK`)**: Daftar semua utang dengan status `unpaid` terlihat.
3. Coba filter lain: `GET /api/supplierdebts` (tanpa filter — semua status).

### D2. Lihat Utang Berdasarkan PO
1. Akses endpoint: `GET /api/supplierdebts/by-po/{PO_TEMPO_ID}`
2. **Respon Sukses (`200 OK`)**: Detail utang untuk PO Tempo yang baru diselesaikan.

### D3. Bayar Utang Secara Parsial
1. Akses endpoint: `POST /api/supplierdebts/pay`
2. Kirim payload pembayaran sebagian (bayar Rp 60.000 dari total utang Rp 150.000):
   ```json
   {
     "purchaseOrderId": "{PO_TEMPO_ID}",
     "amount": 60000,
     "paymentMethod": "Transfer Bank",
     "referenceNumber": "TRF-2026080201"
   }
   ```
3. **Respon Sukses (`200 OK`)**: Record pembayaran berhasil dibuat.
4. **Verifikasi Saldo Utang**: Panggil `GET /api/supplierdebts/by-po/{PO_TEMPO_ID}`:
   - `paidAmount` harus bernilai `60000`.
   - `remainingAmount` harus bernilai `90000`.
   - `status` harus berubah menjadi `"partially_paid"`.

### D4. Uji Pembayaran Melebihi Sisa Utang (Overpayment Guard)
1. Coba bayar dengan jumlah melebihi sisa utang (`90000`):
   ```json
   {
     "purchaseOrderId": "{PO_TEMPO_ID}",
     "amount": 200000,
     "paymentMethod": "Cash",
     "referenceNumber": null
   }
   ```
2. **Respon Error**: Sistem harus menolak dengan pesan *"Jumlah pembayaran... melebihi sisa utang..."*.

### D5. Lunasi Utang (Full Payment)
1. Akses endpoint: `POST /api/supplierdebts/pay`
2. Kirim payload pelunasan sisa utang `90000`:
   ```json
   {
     "purchaseOrderId": "{PO_TEMPO_ID}",
     "amount": 90000,
     "paymentMethod": "Cash",
     "referenceNumber": null
   }
   ```
3. **Respon Sukses (`200 OK`)**.
4. **Verifikasi Lunas**: Panggil kembali `GET /api/supplierdebts/by-po/{PO_TEMPO_ID}`:
   - `paidAmount` harus bernilai `150000`.
   - `remainingAmount` harus bernilai `0`.
   - `status` harus berubah menjadi `"paid"`.

### D6. Uji Pembayaran pada Utang yang Sudah Lunas (Duplicate Payment Guard)
1. Coba bayar kembali utang yang sudah `paid`:
   ```json
   {
     "purchaseOrderId": "{PO_TEMPO_ID}",
     "amount": 10000,
     "paymentMethod": "Cash",
     "referenceNumber": null
   }
   ```
2. **Respon Error**: Sistem harus menolak dengan pesan *"Utang ini sudah lunas"*.

### D7. Lihat Riwayat Semua Pembayaran
1. Akses endpoint: `GET /api/supplierdebts/payments`
2. **Respon Sukses (`200 OK`)**: Semua riwayat pembayaran utang (D3 dan D5) muncul, terurut dari yang terbaru.

---

## Ringkasan Verifikasi Final

| # | Skenario | Ekspektasi |
|---|----------|------------|
| A1 | Buat Supplier | `201 Created`, `isActive: true` |
| A3 | Nonaktifkan Supplier | Tidak muncul di daftar aktif |
| B1 | Buat PO Cash | `status: "draft"`, `totalAmount` tepat |
| B3 | Selesaikan PO Cash | Stok bertambah, HPP ter-update, **tidak ada utang** |
| B4 | Complete PO yang sudah Completed | Error: tidak bisa diubah kembali |
| C1 | Tempo tanpa DueDate | Error: DueDate wajib diisi |
| C3 | Selesaikan PO Tempo | Stok bertambah, HPP ter-update, **utang otomatis terbentuk** |
| D3 | Bayar parsial | `status: "partially_paid"`, saldo berkurang tepat |
| D4 | Bayar melebihi sisa utang | Error: melebihi sisa utang |
| D5 | Lunasi utang | `status: "paid"`, `remainingAmount: 0` |
| D6 | Bayar utang yang sudah lunas | Error: sudah lunas |
