# Accounting Module Roadmap - MorrusPOS

Dokumen ini menjadi acuan implementasi modul akuntansi ringan di MorrusPOS untuk mencakup:

1. Fitur A: fondasi akuntansi bisnis
2. Fitur B: pemasukan dan pengeluaran toko manual
3. Integrasi laporan arus kas dan laba rugi
4. Integrasi bertahap dengan modul POS yang sudah ada

Dokumen ini disusun agar pengerjaan tetap teratur walaupun target akhirnya adalah mengerjakan A dan B sekaligus dalam satu roadmap.

---

## 1. Tujuan Modul

Target akhir modul ini:

- User bisa input pemasukan toko
- User bisa input pengeluaran toko
- Sistem menyimpan transaksi input manual ke `cash_flows`
- Sistem otomatis membuat jurnal ke `account_transactions`
- Laporan arus kas membaca data dari `account_transactions`
- Laporan laba rugi membaca data dari `account_transactions`
- Modul POS existing bisa diintegrasikan bertahap ke jurnal akuntansi yang sama

Dengan pendekatan ini, laporan keuangan tidak lagi hanya bergantung pada transaksi penjualan POS, tetapi sudah punya fondasi akuntansi operasional toko.

---

## 2. Kondisi Sistem Saat Ini

Berdasarkan struktur backend dan frontend MorrusPOS saat ini:

- Sudah ada laporan laba rugi
- Laporan laba rugi saat ini masih dihitung dari `transactions` dan `transaction_items`
- Belum ada entitas `cash_flows`
- Belum ada entitas `account_transactions`
- Belum ada master `chart_of_accounts`
- Belum ada laporan arus kas
- Belum ada menu pemasukan toko dan pengeluaran toko

Implikasinya: fitur ini bukan patch kecil, tetapi penambahan modul baru yang terintegrasi dengan laporan existing.

---

## 3. Alur Bisnis Target

### 3.1 Alur besar

1. User input transaksi di menu Pendapatan Toko atau Pengeluaran Toko
2. Data disimpan ke tabel `cash_flows`
3. Setelah tersimpan, sistem membuat nomor transaksi
4. Sistem otomatis membuat catatan jurnal ke `account_transactions`
5. Menu laporan membaca `account_transactions`, bukan langsung `cash_flows`
6. Laporan arus kas mengambil transaksi akun kas atau bank
7. Laporan laba rugi mengambil transaksi entitas bisnis dan mengelompokkan berdasarkan tipe akun

### 3.2 Alur pemasukan toko

User input:

- `trx_date`
- `from_chart_of_account_id` opsional
- `to_chart_of_account_id` wajib
- `in_amount`
- `note`
- `attachment`

Saat submit:

- sistem set `trx_type = in`
- sistem set `trx_entity = business`
- sistem set `business_id`
- sistem set `outlet_id` atau `branch_id` jika dipakai
- data masuk ke `cash_flows`

Setelah tersimpan:

- generate `trx_number`
- buat jurnal ke `account_transactions`

Efek ke laporan:

- jika akun terkait termasuk kas atau bank, transaksi muncul di laporan arus kas
- jika akun lawan bertipe `revenue`, nilainya ikut ke laba rugi

### 3.3 Alur pengeluaran toko

User input:

- `trx_date`
- `from_chart_of_account_id` wajib
- `to_chart_of_account_id` opsional
- `out_amount`
- `note`
- `attachment`

Saat submit:

- sistem set `trx_type = out`
- sistem set `trx_entity = business`
- sistem set `business_id`
- sistem set `outlet_id` atau `branch_id` jika dipakai
- data masuk ke `cash_flows`

Setelah tersimpan:

- generate `trx_number`
- buat jurnal ke `account_transactions`

Efek ke laporan:

- jika akun asal adalah akun kas atau bank, muncul sebagai kas keluar di laporan arus kas
- jika akun lawan bertipe `expense`, nilainya masuk ke laba rugi sebagai biaya

---

## 4. Komponen Data Yang Dibutuhkan

### 4.1 `chart_of_accounts`

Master akun untuk menentukan klasifikasi akuntansi.

Kolom minimum yang disarankan:

- `id`
- `business_id`
- `outlet_id` nullable jika akun bisa global bisnis
- `account_code`
- `account_name`
- `account_type`
- `is_cash_bank`
- `is_active`
- `parent_account_id` nullable
- `created_at`
- `updated_at`

Nilai `account_type` minimum:

- `asset`
- `liability`
- `equity`
- `revenue`
- `cogs`
- `expense`

Contoh akun seed awal:

- Kas
- Bank BCA
- Bank Mandiri
- Pendapatan Lain-lain
- Biaya Operasional
- Biaya Transport
- Biaya Listrik
- HPP
- Utang Supplier

### 4.2 `cash_flows`

Sumber input transaksi manual dari user.

Kolom minimum yang disarankan:

- `id`
- `business_id`
- `outlet_id` nullable
- `trx_number`
- `trx_date`
- `trx_type`
- `trx_entity`
- `from_chart_of_account_id` nullable
- `to_chart_of_account_id` nullable
- `amount`
- `note`
- `attachment_url`
- `created_by`
- `created_at`
- `updated_at`

Nilai minimum:

- `trx_type`: `in`, `out`
- `trx_entity`: `business`

### 4.3 `account_transactions`

Sumber utama seluruh laporan akuntansi.

Kolom minimum yang disarankan:

- `id`
- `business_id`
- `outlet_id` nullable
- `trx_date`
- `trx_number`
- `reference_type`
- `reference_id`
- `trx_entity`
- `chart_of_account_id`
- `debit_amount`
- `credit_amount`
- `note`
- `created_at`

Catatan:

- Jika ingin desain lebih rapi, bisa pecah menjadi header-detail:
  - `account_transaction_headers`
  - `account_transaction_lines`
- Untuk fase awal, satu tabel jurnal line juga cukup selama satu transaksi bisa menghasilkan minimal dua baris

---

## 5. Prinsip Posting Jurnal

Supaya laporan bisa konsisten, setiap transaksi `cash_flows` harus menghasilkan jurnal minimal dua sisi.

### 5.1 Pemasukan toko

Contoh:

- debit akun kas atau bank
- credit akun pendapatan

### 5.2 Pengeluaran toko

Contoh:

- debit akun biaya
- credit akun kas atau bank

### 5.3 Aturan minimum

- tidak boleh ada jurnal satu sisi
- total debit harus sama dengan total credit
- akun kas atau bank harus ditandai lewat `is_cash_bank = true`
- akun untuk laba rugi harus dibaca dari `account_type`

---

## 6. Sumber Data Laporan

### 6.1 Laporan arus kas

Sumber:

- `account_transactions`

Filter utama:

- `trx_entity = business`
- akun bertipe `asset`
- `is_cash_bank = true`
- filter tanggal
- filter `business_id`
- filter `outlet_id` jika nanti dibutuhkan versi per cabang

Output minimum:

- kas awal
- kas masuk
- kas keluar
- kas akhir
- saldo berjalan

### 6.2 Laporan laba rugi

Sumber:

- `account_transactions`

Filter utama:

- `trx_entity = business`
- filter tanggal
- filter `business_id`
- filter `outlet_id` opsional

Pengelompokan:

- `revenue` menjadi pendapatan
- `cogs` menjadi HPP
- `expense` menjadi biaya

Rumus:

- laba kotor = revenue - cogs
- laba bersih = laba kotor - expense

Catatan penting:

- Laporan laba rugi existing saat ini masih berbasis transaksi penjualan POS
- Saat modul ini aktif, perlu diputuskan apakah:
  - laporan lama diganti penuh
  - laporan lama tetap ada sebagai laporan operasional penjualan
  - dibuat laporan laba rugi akuntansi baru terpisah

Rekomendasi:

- pertahankan laporan existing sebagai "Laporan Penjualan dan Margin"
- buat "Laporan Laba Rugi Akuntansi" sebagai laporan keuangan resmi

---

## 7. Strategi Integrasi Dengan Sistem Existing

Roadmap ini menggabungkan A dan B, tetapi implementasi harus bertahap agar stabil.

### 7.1 Yang dibuat lebih dulu

- master akun
- input manual pemasukan dan pengeluaran
- posting jurnal otomatis
- laporan arus kas
- laporan laba rugi akuntansi

### 7.2 Yang diintegrasikan belakangan

- transaksi POS
- pembelian supplier atau purchase order
- pembayaran utang supplier
- retur supplier
- settlement channel
- konsinyasi

Tujuannya agar fondasi akuntansi jadi dulu, baru modul operasional lain ikut masuk ke jurnal.

---

## 8. Fase Pengerjaan

## Fase 1 - Fondasi Data dan Domain Akuntansi

Tujuan:

- menyiapkan struktur data inti
- menyiapkan enum, entity, dan konfigurasi EF Core

Pekerjaan backend:

- tambah entity `ChartOfAccount`
- tambah entity `CashFlow`
- tambah entity `AccountTransaction`
- tambah enum atau konstanta:
  - `account_type`
  - `cash_flow_type`
  - `transaction_entity`
- tambahkan `DbSet` baru ke `AppDbContext`
- buat konfigurasi mapping EF Core
- buat migration database
- buat seed akun default per business atau template akun default

Pekerjaan frontend:

- belum fokus UI penuh
- cukup siapkan type dan contract jika dibutuhkan paralel

Output fase:

- database siap menampung modul akuntansi dasar

Checklist verifikasi:

- migration sukses dijalankan
- tabel baru terbentuk
- akun default tersimpan
- data tenant tetap scoped by `BusinessId`

---

## Fase 2 - Master Chart of Accounts

Tujuan:

- user keuangan bisa mengelola akun

Pekerjaan backend:

- CRUD akun
- validasi `account_code` unik per business
- validasi akun inactive tidak bisa dipakai transaksi baru
- permission untuk owner, admin, keuangan

Pekerjaan frontend:

- halaman daftar akun
- form tambah akun
- form edit akun
- filter akun aktif
- badge tipe akun
- penanda `is_cash_bank`

Output fase:

- user bisa menyiapkan daftar akun sebelum input transaksi manual

Checklist verifikasi:

- akun bisa dibuat, diubah, dinonaktifkan
- akun kas atau bank terdeteksi benar
- akun `revenue`, `cogs`, `expense` muncul sesuai tipenya

---

## Fase 3 - Pemasukan dan Pengeluaran Toko Manual

Tujuan:

- user bisa input transaksi manual operasional toko

Pekerjaan backend:

- endpoint create pemasukan toko
- endpoint create pengeluaran toko
- endpoint list histori transaksi
- endpoint detail transaksi
- upload attachment
- generate `trx_number`
- simpan ke `cash_flows`

Pekerjaan frontend:

- menu `Pendapatan Toko`
- menu `Pengeluaran Toko`
- form input transaksi
- halaman histori
- filter tanggal, akun, outlet, nominal, keyword

Aturan bisnis minimum:

- pemasukan: `to_chart_of_account_id` wajib
- pengeluaran: `from_chart_of_account_id` wajib
- nominal harus lebih besar dari 0
- tanggal transaksi wajib
- attachment opsional

Output fase:

- transaksi manual bisa dicatat user

Checklist verifikasi:

- pemasukan berhasil tersimpan ke `cash_flows`
- pengeluaran berhasil tersimpan ke `cash_flows`
- nomor transaksi tergenerate unik
- histori tampil sesuai filter

---

## Fase 4 - Posting Jurnal Otomatis

Tujuan:

- setiap transaksi manual otomatis masuk ke jurnal

Pekerjaan backend:

- buat service posting jurnal
- panggil service setelah `cash_flows` tersimpan
- pastikan satu transaksi menghasilkan dua atau lebih line jurnal
- validasi keseimbangan debit dan credit
- simpan ke `account_transactions`

Catatan desain:

- di .NET current architecture, lebih aman memakai service domain atau application service
- tidak wajib meniru observer Laravel secara literal
- yang penting perilaku bisnisnya sama: setelah transaksi tersimpan, jurnal ikut terbentuk

Contoh posting:

Pemasukan:

- debit kas atau bank
- credit pendapatan

Pengeluaran:

- debit biaya
- credit kas atau bank

Output fase:

- `cash_flows` dan `account_transactions` sudah terhubung

Checklist verifikasi:

- setiap transaksi manual menghasilkan jurnal
- debit dan credit seimbang
- referensi `reference_type` dan `reference_id` tersimpan benar

---

## Fase 5 - Laporan Arus Kas

Tujuan:

- user bisa melihat arus kas berdasarkan jurnal akuntansi

Pekerjaan backend:

- buat query laporan arus kas
- hitung kas awal sebelum periode
- hitung kas masuk periode
- hitung kas keluar periode
- hitung kas akhir
- siapkan export CSV atau Excel

Pekerjaan frontend:

- halaman laporan arus kas
- filter tanggal
- filter outlet jika diperlukan
- tabel mutasi kas
- kartu ringkasan kas awal, masuk, keluar, akhir

Output fase:

- tersedia laporan arus kas yang tidak lagi bergantung pada tabel input manual

Checklist verifikasi:

- transaksi akun kas atau bank muncul di laporan
- kas masuk dan keluar dihitung benar
- saldo akhir sesuai akumulasi jurnal

---

## Fase 6 - Laporan Laba Rugi Akuntansi

Tujuan:

- menampilkan laba rugi berbasis akun, bukan hanya penjualan POS

Pekerjaan backend:

- buat query laba rugi dari `account_transactions`
- kelompokkan by `account_type`
- hitung revenue
- hitung cogs
- hitung expense
- hitung laba kotor
- hitung laba bersih
- siapkan export CSV atau Excel

Pekerjaan frontend:

- buat halaman baru atau revisi halaman existing
- tampilkan blok:
  - pendapatan
  - HPP
  - biaya
  - laba kotor
  - laba bersih

Keputusan produk yang perlu disepakati:

- apakah route lama `reports/profit-loss` diganti total
- atau dibuat route baru misalnya `reports/profit-loss-accounting`

Rekomendasi implementasi:

- buat route baru dulu
- setelah validasi bisnis selesai, baru putuskan apakah laporan lama dipensiunkan

Output fase:

- laba rugi akuntansi tersedia untuk tim keuangan

Checklist verifikasi:

- akun `revenue` terbaca sebagai pendapatan
- akun `cogs` terbaca sebagai HPP
- akun `expense` terbaca sebagai biaya
- rumus laba bersih sesuai

---

## Fase 7 - Integrasi Modul Existing Ke Jurnal

Tujuan:

- agar jurnal akuntansi tidak hanya berasal dari input manual

Target integrasi bertahap:

1. transaksi penjualan POS
2. purchase order selesai atau barang diterima
3. pembayaran utang supplier
4. retur supplier
5. settlement channel
6. konsinyasi dan settlement konsinyasi

Contoh arah mapping:

- penjualan POS:
  - debit kas atau piutang channel
  - credit revenue
  - debit cogs
  - credit persediaan atau akun stok terkait

- pembayaran supplier:
  - debit utang usaha
  - credit kas atau bank

Catatan:

- fase ini paling sensitif
- jangan dijalankan sebelum fase 1 sampai 6 stabil

Output fase:

- laporan keuangan mulai mencerminkan aktivitas bisnis lebih lengkap

Checklist verifikasi:

- transaksi operasional existing menghasilkan jurnal sesuai mapping
- tidak ada double posting
- laporan tidak melonjak ganda karena source lama dan source baru tercampur

---

## 9. Pembagian API Yang Disarankan

### Master akun

- `GET /api/chart-of-accounts`
- `POST /api/chart-of-accounts`
- `PUT /api/chart-of-accounts/{id}`
- `DELETE /api/chart-of-accounts/{id}` atau soft delete

### Cash flow manual

- `GET /api/cash-flows`
- `GET /api/cash-flows/{id}`
- `POST /api/cash-flows/income-business`
- `POST /api/cash-flows/outcome-business`

### Reports

- `GET /api/reports/cash-flow`
- `GET /api/reports/cash-flow/export-excel`
- `GET /api/reports/profit-loss-accounting`
- `GET /api/reports/profit-loss-accounting/export-excel`

---

## 10. Permission Yang Disarankan

Role minimum:

- `Owner`
- `Admin`
- `Keuangan`

Permission minimum:

- `account.manage`
- `cashflow.create`
- `cashflow.view`
- `cashflow.approve` jika nanti ada approval
- `report.cashflow.view`
- `report.profitloss_accounting.view`

Jika ingin lebih aman:

- kasir tidak boleh input pengeluaran
- kepala cabang hanya boleh lihat outlet sendiri
- owner dan admin bisa lihat seluruh business

---

## 11. Risiko Implementasi

### Risiko 1 - bentrok dengan laporan existing

Masalah:

- laporan laba rugi sekarang berbasis transaksi POS

Mitigasi:

- buat laporan akuntansi baru dulu
- jangan langsung ganti laporan lama

### Risiko 2 - mapping jurnal salah

Masalah:

- satu transaksi bisa masuk ke akun yang salah sehingga laporan salah total

Mitigasi:

- definisikan aturan posting tertulis
- review mapping bersama user bisnis sebelum coding integrasi fase 7

### Risiko 3 - double count

Masalah:

- penjualan POS bisa terhitung di laporan lama dan juga jurnal baru

Mitigasi:

- pisahkan sumber laporan selama masa transisi
- beri label jelas pada setiap laporan

### Risiko 4 - struktur multi outlet

Masalah:

- perlu diputuskan apakah akun per business atau per outlet

Mitigasi:

- default akun di level business
- `outlet_id` dijadikan nullable pada transaksi untuk kebutuhan breakdown cabang

---

## 12. Rekomendasi Implementasi Praktis

Agar tetap realistis, urutan kerja yang direkomendasikan:

1. Fase 1 sampai 4 terlebih dahulu
2. lanjut Fase 5 dan 6 agar user langsung dapat manfaat laporan
3. baru Fase 7 untuk integrasi penuh modul existing

Dengan urutan ini:

- user cepat mendapat fitur pemasukan dan pengeluaran toko
- tim keuangan cepat mendapat arus kas dan laba rugi akuntansi
- tim engineering tidak terbebani integrasi besar di awal

---

## 13. Definisi Selesai

Modul dianggap selesai minimum jika:

- master akun sudah tersedia
- pemasukan toko dan pengeluaran toko bisa diinput
- transaksi manual tersimpan di `cash_flows`
- jurnal otomatis tersimpan di `account_transactions`
- laporan arus kas berjalan
- laporan laba rugi akuntansi berjalan
- permission dan tenant scoping aman

Modul dianggap selesai penuh jika:

- transaksi POS dan modul operasional utama lain juga ikut posting jurnal
- laporan keuangan bisnis sudah konsisten end-to-end

---

## 14. Ringkasan Eksekusi

Roadmap ini menggabungkan fitur A dan B dalam satu arah besar:

- bangun fondasi akuntansi dulu
- aktifkan input manual pemasukan dan pengeluaran
- jadikan jurnal sebagai sumber utama laporan
- integrasikan modul existing secara bertahap

Pendekatan ini paling aman untuk MorrusPOS karena sistem yang ada sekarang belum memiliki domain akuntansi umum, tetapi sudah punya struktur layanan, laporan, dan multitenancy yang cukup baik untuk diperluas.
