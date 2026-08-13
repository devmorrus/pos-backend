# Panduan Manual Testing Akuntansi - Fase 1 sampai Fase 7

Dokumen ini adalah panduan manual testing end-to-end untuk modul akuntansi MorrusPOS dari fase 1 sampai fase 7, dengan fokus pada:

1. fondasi data akuntansi
2. master `Chart of Accounts`
3. pemasukan dan pengeluaran toko manual
4. sumber jurnal `account_transactions`
5. laporan arus kas akuntansi
6. laporan laba rugi akuntansi
7. integrasi modul existing ke jurnal akuntansi

Dokumen ini ditujukan untuk developer agar bisa menguji dari nol secara runut, baik di local maupun staging.

---

## 1. Tujuan Pengujian

Setelah seluruh skenario di dokumen ini selesai, hasil yang diharapkan adalah:

- akun akuntansi dapat dibuat dan dikelola
- transaksi manual masuk ke `cash_flows`
- setiap transaksi manual membuat jurnal ke `account_transactions`
- laporan arus kas membaca jurnal akun kas/bank
- laporan laba rugi membaca jurnal akun `revenue`, `cogs`, dan `expense`
- transaksi existing seperti penjualan POS, PO, pembayaran supplier, retur supplier, settlement channel, dan settlement konsinyasi ikut membentuk jurnal otomatis
- finance/admin dapat memonitor status integrasi akuntansi dari frontend

---

## 2. Prasyarat Umum

1. Pastikan backend berjalan:
   ```bash
   cd backend
   dotnet run --project src/MorrusPOS.Api
   ```
2. Pastikan frontend berjalan:
   ```bash
   cd frontend
   npm run dev
   ```
3. Pastikan database sudah termigrasi dan akun login memiliki `business_id`.
4. Gunakan akun dengan role `Owner`, `Admin`, atau `Keuangan`.
5. Siapkan minimal satu outlet aktif.
6. Siapkan minimal satu supplier aktif.
7. Siapkan minimal dua produk aktif:
   - satu produk reguler
   - satu produk yang nanti dipakai untuk konsinyasi

---

## 3. Checklist Data Awal

Sebelum mulai fase 1 sampai 7, siapkan akun-akun berikut di `Chart of Accounts`.

Disarankan membuat akun business-level dulu:

- `1010` Kas Utama
- `1020` Bank Utama
- `1200` Persediaan Barang
- `1300` Piutang Channel GrabFood
- `2100` Utang Supplier
- `2200` Utang Konsinyasi
- `4100` Pendapatan Penjualan
- `4200` Pendapatan Toko Lain-lain
- `5100` Harga Pokok Penjualan
- `6100` Biaya Operasional
- `6200` Biaya Fee Channel

Catatan validasi:

- `Kas Utama` dan `Bank Utama` harus `accountType = asset` dan `isCashBank = true`
- `Persediaan Barang` harus `accountType = asset` dan `isCashBank = false`
- `Piutang Channel GrabFood` harus `accountType = asset` dan `isCashBank = false`
- `Utang Supplier` dan `Utang Konsinyasi` harus `accountType = liability`
- `Pendapatan Penjualan` dan `Pendapatan Toko Lain-lain` harus `accountType = revenue`
- `Harga Pokok Penjualan` harus `accountType = cogs`
- `Biaya Operasional` dan `Biaya Fee Channel` harus `accountType = expense`

---

## 4. Fase 1 - Fondasi Accounting Backend

Fase ini fokus memastikan fondasi data dan context business berjalan.

### Skenario 1. Business context tersedia

1. Login ke aplikasi dengan akun `Owner`, `Admin`, atau `Keuangan`.
2. Buka halaman:
   - `/chart-of-accounts`
   - `/income-businesses`
   - `/reports/cash-flow`
3. Pastikan halaman tidak menampilkan error `Business context tidak ditemukan`.

Expected result:

- request ke API accounting tidak gagal `401/403` karena `business_id` kosong

### Skenario 2. Tabel accounting dapat dipakai

1. Buka database.
2. Pastikan tabel ini ada:
   - `chart_of_accounts`
   - `cash_flows`
   - `account_transactions`

Expected result:

- seluruh tabel accounting sudah tersedia

---

## 5. Fase 2 - Master Chart of Accounts

Fase ini fokus ke CRUD master akun.

### Skenario 1. Buka halaman COA

1. Login sebagai `Owner`, `Admin`, atau `Keuangan`.
2. Buka `/chart-of-accounts`.

Expected result:

- halaman `Chart of Accounts` tampil
- tombol `Tambah akun` tampil
- filter code/name, type, scope, status, outlet tampil

### Skenario 2. Buat akun business-level

1. Klik `Tambah akun`.
2. Isi:
   - `Kode Akun`: `1010`
   - `Nama Akun`: `Kas Utama`
   - `Tipe`: `asset`
   - `Scope`: `Business`
   - `Cash/Bank`: aktif
3. Submit.

Expected result:

- akun berhasil tersimpan
- akun muncul di tabel
- badge scope menunjukkan `Business`
- badge cash/bank menunjukkan `Kas/Bank`

### Skenario 3. Buat akun outlet-level

1. Klik `Tambah akun`.
2. Isi:
   - `Kode Akun`: `6101`
   - `Nama Akun`: `Biaya Outlet A`
   - `Tipe`: `expense`
   - `Scope`: `Outlet`
   - pilih outlet aktif
   - `Cash/Bank`: nonaktif
3. Submit.

Expected result:

- akun berhasil tersimpan
- scope tampil sebagai `Outlet: {nama outlet}`

### Skenario 4. Validasi akun duplicate

1. Coba buat lagi akun dengan `Kode Akun = 1010` dalam business yang sama.

Expected result:

- API menolak
- UI menampilkan error duplicate `accountCode`

### Skenario 5. Validasi cash/bank non-asset

1. Buat akun baru:
   - `accountType = expense`
   - `isCashBank = true`

Expected result:

- checkbox cash/bank disabled saat type bukan `asset`
- jika dipaksa dari API, request ditolak

### Skenario 6. Edit dan nonaktifkan akun

1. Edit salah satu akun.
2. Ubah nama atau status `isActive = false`.

Expected result:

- akun berhasil diperbarui
- badge status berubah `Nonaktif`
- akun nonaktif tetap tampil jika filter status mengizinkan

---

## 6. Fase 3 - Pendapatan dan Pengeluaran Toko Manual

Fase ini fokus ke `cash_flows` manual dan posting jurnal otomatis.

### Skenario 1. Buat pemasukan toko manual

1. Buka `/income-businesses/create`.
2. Isi:
   - `Tanggal`: hari ini
   - `Outlet`: kosong atau pilih outlet sesuai scope akun
   - `From Account`: `Kas Utama`
   - `To Account`: `Pendapatan Toko Lain-lain`
   - `Amount`: `150000`
   - `Note`: `Pemasukan manual testing`
3. Submit.

Expected result:

- transaksi berhasil dibuat
- nomor transaksi berformat `CFI-{yyyyMMdd}-{NNNN}`
- detail transaksi bisa dibuka

### Skenario 2. Verifikasi jurnal pemasukan

1. Ambil `id` cash flow hasil skenario 1.
2. Buka detail `/income-businesses/{id}`.

Expected result:

- tampil 2 line jurnal
- `Kas Utama` debit `150000`
- `Pendapatan Toko Lain-lain` credit `150000`

### Skenario 3. Buat pengeluaran toko manual

1. Buka `/outcome-businesses/create`.
2. Isi:
   - `Tanggal`: hari ini
   - `From Account`: `Kas Utama`
   - `To Account`: `Biaya Operasional`
   - `Amount`: `50000`
   - `Note`: `Pengeluaran manual testing`
3. Submit.

Expected result:

- transaksi berhasil dibuat
- nomor transaksi berformat `CFO-{yyyyMMdd}-{NNNN}`

### Skenario 4. Verifikasi jurnal pengeluaran

1. Buka detail transaksi outcome.

Expected result:

- tampil 2 line jurnal
- `Biaya Operasional` debit `50000`
- `Kas Utama` credit `50000`

### Skenario 5. Validasi aturan kas/bank

1. Coba submit income atau outcome dengan dua akun yang sama-sama bukan kas/bank.

Expected result:

- API menolak dengan pesan bahwa salah satu akun harus kas/bank

### Skenario 6. Upload attachment

1. Buat transaksi manual baru dengan attachment `.jpg` atau `.pdf`.

Expected result:

- upload sukses
- URL attachment tersimpan
- link attachment tampil di detail transaksi

---

## 7. Fase 4 - Validasi Sumber Jurnal Utama

Fase ini memastikan laporan tidak membaca `cash_flows` langsung, tapi membaca `account_transactions`.

### Skenario 1. Verifikasi source data

1. Buat satu pemasukan manual.
2. Catat:
   - row baru di `cash_flows`
   - 2 row baru di `account_transactions`

Expected result:

- `cash_flows` hanya menyimpan sumber input
- `account_transactions` menyimpan jurnal final

### Skenario 2. Validasi referensi jurnal

1. Lihat row `account_transactions` hasil cash flow manual.

Expected result:

- `referenceType = cash_flow`
- `referenceId = {cash_flow_id}`
- `trxEntity = business`

---

## 8. Fase 5 - Laporan Arus Kas Akuntansi

Fase ini fokus ke `/reports/cash-flow`.

### Skenario 1. Buka halaman laporan arus kas

1. Login sebagai user yang punya permission `report.cashflow.view`.
2. Buka `/reports/cash-flow`.

Expected result:

- halaman tampil
- filter tanggal, outlet, akun, keyword tampil
- summary card tampil:
  - `Kas Awal`
  - `Kas Masuk`
  - `Kas Keluar`
  - `Kas Akhir`

### Skenario 2. Verifikasi transaksi manual masuk ke arus kas

1. Gunakan data phase 3:
   - pemasukan `150000`
   - pengeluaran `50000`
2. Filter periode yang mencakup dua transaksi tadi.

Expected result:

- `Kas Masuk` bertambah `150000`
- `Kas Keluar` bertambah `50000`
- `Kas Akhir` sesuai perhitungan
- tabel menampilkan line akun kas/bank saja

### Skenario 3. Filter akun kas/bank

1. Gunakan filter `chartOfAccountId = Kas Utama`.

Expected result:

- hanya line yang terkait akun kas utama yang tampil

### Skenario 4. Export Excel arus kas

1. Klik `Ekspor Excel`.

Expected result:

- file `.xlsx` terdownload
- judul, periode, summary, dan line transaksi sesuai filter aktif

---

## 9. Fase 6 - Laporan Laba Rugi Akuntansi

Fase ini fokus ke `/reports/profit-loss`.

### Skenario 1. Buka halaman laporan laba rugi

1. Login sebagai user yang punya permission `report.profitloss_accounting.view`.
2. Buka `/reports/profit-loss`.

Expected result:

- halaman tampil
- filter tanggal, outlet, keyword tampil
- summary card tampil:
  - `Pendapatan`
  - `HPP`
  - `Laba Kotor`
  - `Biaya`
  - `Laba Bersih`

### Skenario 2. Verifikasi pemasukan manual masuk ke revenue

1. Gunakan pemasukan manual phase 3 dengan akun tujuan `Pendapatan Toko Lain-lain`.
2. Filter periode yang sesuai.

Expected result:

- section `Pendapatan` menampilkan akun revenue tersebut
- nominal = credit - debit

### Skenario 3. Verifikasi pengeluaran manual masuk ke expense

1. Gunakan pengeluaran manual phase 3 dengan akun lawan `Biaya Operasional`.

Expected result:

- section `Biaya Operasional` menampilkan akun expense tersebut
- nominal = debit - credit

### Skenario 4. Verifikasi rumus summary

Expected result:

- `Laba Kotor = Pendapatan - HPP`
- `Laba Bersih = Laba Kotor - Biaya`

### Skenario 5. Export Excel laba rugi

1. Klik `Ekspor Excel`.

Expected result:

- file `.xlsx` terdownload
- summary dan section `Pendapatan`, `HPP`, `Biaya` sesuai filter aktif

---

## 10. Fase 7 - Integrasi Modul Existing ke Jurnal

Fase ini fokus pada posting otomatis dari transaksi existing ke `account_transactions`.

## 10.1 Frontend Monitoring

### Skenario 1. Buka halaman monitor integrasi

1. Login sebagai `Owner`, `Admin`, atau `Keuangan`.
2. Buka `/accounting-integrations`.

Expected result:

- halaman tampil
- blok `Cek status posting` tampil
- blok `Backfill integrasi` tampil

### Skenario 2. Cek status posting manual

1. Masukkan:
   - `referenceType`
   - `referenceId`
2. Klik `Cek status`.

Expected result:

- jika jurnal sudah ada, status `Sudah terjurnal`
- jika belum ada, status `Belum terjurnal`
- jumlah line jurnal tampil

### Skenario 3. Jalankan backfill

1. Pilih tanggal bila perlu.
2. Centang modul yang ingin dibackfill.
3. Klik `Jalankan backfill`.

Expected result:

- summary hasil backfill muncul
- total source terposting bertambah sesuai data

## 10.2 Penjualan POS

### Skenario 4. Checkout POS reguler

1. Buka `/pos`.
2. Lakukan transaksi untuk produk reguler.
3. Selesaikan pembayaran.
4. Buka detail transaksi `/transactions/{id}`.

Expected result:

- badge `Sudah terjurnal` tampil di detail transaksi
- `referenceType = transaction_sale`

### Skenario 5. Verifikasi laporan dari sales POS

Expected result:

- laporan arus kas bertambah jika pembayaran tunai
- laporan laba rugi bertambah di:
  - `Pendapatan`
  - `HPP`

## 10.3 Purchase Order

### Skenario 6. Selesaikan PO cash atau tempo

1. Buat PO di `/purchase-orders/create`.
2. Selesaikan sampai status `completed`.
3. Buka detail PO.

Expected result:

- badge status jurnal tampil di detail PO
- jika PO `tempo`, jurnal:
  - debit `Persediaan Barang`
  - credit `Utang Supplier`
- jika PO `cash`, jurnal:
  - debit `Persediaan Barang`
  - credit `Kas/Bank`

## 10.4 Pembayaran Utang Supplier

### Skenario 7. Bayar utang supplier

1. Buka `/supplier-debts`.
2. Bayar sebagian atau penuh.

Expected result:

- row pembayaran supplier tercatat
- jurnal terbentuk:
  - debit `Utang Supplier`
  - credit `Kas/Bank`

Catatan:

- bila belum ada badge khusus di halaman pembayaran, verifikasi via `/accounting-integrations`

## 10.5 Retur Supplier

### Skenario 8. Kirim retur supplier

1. Buat retur supplier draft.
2. Ubah status menjadi `sent`.
3. Buka detail retur supplier.

Expected result:

- badge `Sudah terjurnal` tampil
- jurnal:
  - debit `Utang Supplier`
  - credit `Persediaan Barang`

## 10.6 Settlement Channel

### Skenario 9. Selesaikan settlement channel

1. Buat settlement channel.
2. Ubah status jadi `settled`.
3. Buka detail settlement.

Expected result:

- badge `Sudah terjurnal` tampil
- jurnal:
  - debit `Bank Utama` sebesar net amount
  - debit `Biaya Fee Channel` sebesar fee
  - credit `Piutang Channel GrabFood` sebesar gross amount

## 10.7 Settlement Konsinyasi

### Skenario 10. Selesaikan settlement konsinyasi

1. Buat sales konsinyasi sampai ada hak supplier.
2. Buat settlement konsinyasi.
3. Ubah status jadi `settled`.
4. Buka detail settlement konsinyasi.

Expected result:

- badge `Sudah terjurnal` tampil
- jurnal:
  - debit `Utang Konsinyasi`
  - credit `Bank Utama`

---

## 11. Verifikasi Database Akhir

Setelah seluruh fase diuji, lakukan pengecekan akhir di database.

Pastikan tabel berikut terisi secara konsisten:

- `chart_of_accounts`
- `cash_flows`
- `account_transactions`
- `transactions`
- `purchase_orders`
- `supplier_payments`
- `supplier_returns`
- `channel_settlements`
- `consignment_settlements`

Validasi utama:

- tidak ada jurnal satu sisi
- total debit = total credit untuk setiap `referenceType + referenceId`
- tidak ada double posting untuk source transaction yang sama
- laporan phase 5 dan 6 berubah sesuai data phase 3 dan phase 7

---

## 12. Regression Checklist Cepat

Gunakan checklist ini setelah ada perubahan lanjutan.

- login finance tidak error business context
- halaman `Chart of Accounts` bisa dibuka
- create income manual berhasil
- create outcome manual berhasil
- detail cash flow menampilkan jurnal
- laporan arus kas tampil dan export berhasil
- laporan laba rugi tampil dan export berhasil
- checkout POS membuat badge `Sudah terjurnal`
- PO completed membuat jurnal
- supplier payment membuat jurnal
- supplier return membuat jurnal
- channel settlement settled membuat jurnal
- consignment settlement settled membuat jurnal
- halaman `/accounting-integrations` bisa cek status dan backfill

---

## 13. Catatan Untuk Developer

- phase 7 saat ini memakai resolver akun otomatis berdasarkan `accountType`, `isCashBank`, scope outlet, dan keyword nama akun
- untuk hasil terbaik, gunakan nama akun yang konsisten seperti:
  - `Kas`
  - `Bank`
  - `Persediaan`
  - `Utang Supplier`
  - `Utang Konsinyasi`
  - `Piutang Channel`
  - `Biaya Fee Channel`
- jika suatu modul existing belum menampilkan badge khusus di semua halaman list, gunakan `/accounting-integrations` sebagai sumber cek status paling aman

