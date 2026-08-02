# Panduan Manual Testing - Fase 3: Sesi Kasir & Transaksi POS (Real-Time)

Dokumen ini berisi panduan pengujian manual langkah-demi-langkah menggunakan Swagger UI (`/swagger`) dan browser console (untuk SignalR) untuk memverifikasi fitur Sesi Kasir dan Transaksi POS pada Fase 3.

---

## Prasyarat Pengujian
1. Pastikan backend berjalan (`dotnet run` atau `dotnet watch`).
2. Login terlebih dahulu melalui `/api/auth/login` menggunakan akun Kasir atau Admin untuk mendapatkan JWT Token, dan masukkan token tersebut ke tombol **Authorize** di Swagger UI.

---

## Skenario 1: Alur Sesi Kasir (Membuka & Menutup Shift)

### Langkah 1: Buka Sesi Kasir Baru
1. Akses endpoint: `POST /api/cashiersessions/open`
2. Kirim payload berikut (modal awal Rp 100.000):
   ```json
   {
     "openingCash": 100000
   }
   ```
3. **Respon Sukses (`200 OK`)**: Pastikan status sesi bernilai `"open"`, `expectedCash` bernilai `100000`, dan catat `id` sesi kasir tersebut.

### Langkah 2: Cek Sesi Aktif Saat Ini
1. Akses endpoint: `GET /api/cashiersessions/current`
2. **Respon Sukses (`200 OK`)**: Harus mengembalikan detail sesi kasir yang baru saja Anda buka.

### Langkah 3: Buka Sesi Ganda (Pengujian Proteksi)
1. Coba panggil kembali `POST /api/cashiersessions/open` dengan modal awal berapapun.
2. **Respon Error (`400 Bad Request`)**: Sistem harus menolak dengan pesan: *"Anda masih memiliki sesi kasir yang aktif di outlet ini. Harap tutup sesi terlebih dahulu."*

---

## Skenario 2: Transaksi POS (Checkout) & Penyesuaian Stok

### Langkah 1: Siapkan ID Produk dan Stok
1. Ambil salah satu produk dari database. Catat `productId` dan pastikan stok produk tersebut di outlet Anda bernilai (contoh: `10` pcs).

### Langkah 2: Kirim Permintaan Checkout (Pembayaran Tunai / Cash)
1. Akses endpoint: `POST /api/transactions/checkout`
2. Gunakan payload contoh berikut (sesuaikan `outletId`, `cashierSessionId`, dan `productId`). Gunakan `id` acak baru (GUID) sebagai kunci idemponsi:
   ```json
   {
     "id": "e4c5f94d-71b8-466d-92a1-cf0b9f939e6a",
     "outletId": "de305d54-75b4-431b-adb2-2007e52b114d",
     "cashierSessionId": "masukkan-session-id-dari-skenario-1",
     "channel": "pos",
     "subtotal": 20000,
     "discountTotal": 0,
     "taxTotal": 0,
     "grandTotal": 20000,
     "items": [
       {
         "productId": "masukkan-product-id",
         "qty": 2,
         "unitPrice": 10000,
         "discountAmount": 0
       }
     ],
     "payments": [
       {
         "method": "cash",
         "amount": 20000,
         "referenceNumber": null
       }
     ]
   }
   ```
3. **Respon Sukses (`201 Created`)**: Transaksi sukses dibuat dengan nomor transaksi berformat `TRX-yyyyMMddHHmmss-XXXX`.

### Langkah 3: Pengujian Idemponsi (Mencegah Klik Ganda)
1. Klik **Execute** sekali lagi di Swagger dengan payload yang **persis sama** (termasuk GUID `id` transaksi yang sama).
2. **Respon Sukses (`201 Created` / `200 OK`)**: Pastikan transaksi yang dikembalikan adalah transaksi yang sama (nomor TRX sama) tanpa terjadi error duplikat atau pengurangan stok untuk kedua kalinya.

### Langkah 4: Pengujian Stok Kosong / Tidak Cukup
1. Coba kirim checkout transaksi baru dengan GUID `id` berbeda, namun dengan `qty` produk melebihi stok yang tersedia (misal: `100` pcs).
2. **Respon Error (`400 Bad Request`)**: Transaksi harus dibatalkan seketika dengan pesan: *"Stok tidak mencukupi untuk produk [Nama]. Stok tersedia: [Jumlah Stok]"*. Pastikan tidak ada data transaksi parsial yang masuk ke database.

---

## Skenario 3: Penutupan Sesi Kasir & Verifikasi Selisih Uang

### Langkah 1: Tutup Sesi Kasir
1. Akses endpoint: `POST /api/cashiersessions/close/{id}` (masukkan ID sesi kasir di URL).
2. Kirim nominal uang fisik riil yang ada di laci kasir (misal: fisik ada Rp 118.000):
   ```json
   {
     "actualCash": 118000
   }
   ```
3. **Respon Sukses (`200 OK`)**:
   - Status sesi berubah menjadi `"closed"`.
   - `expectedCash` otomatis bernilai `120000` (Rp 100.000 modal + Rp 20.000 transaksi cash).
   - `variance` bernilai `-2000` (selisih kurang Rp 2.000).

---

## Skenario 4: Real-time Stock Update via SignalR (WebSockets)

1. Buka browser console atau alat testing WebSocket, sambungkan ke:
   `wss://localhost:[port]/hub/notifications`
2. Setelah terkoneksi, panggil method Hub `JoinOutletGroup` dengan parameter `outletId` Anda untuk mulai mendengarkan event outlet tersebut.
3. Lakukan transaksi checkout baru di outlet tersebut.
4. Verifikasi bahwa koneksi WebSocket Anda menerima event `"ReceiveStockUpdate"` yang berisi array produk beserta qty yang baru saja berkurang.
