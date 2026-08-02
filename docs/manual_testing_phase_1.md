# Panduan Manual Testing - Fase 1: Keamanan, Hak Akses & Pengguna

Dokumen ini berisi panduan langkah-demi-langkah pengujian manual fitur **Keamanan & Manajemen Pengguna (Fase 1)** pada API MorrusPOS menggunakan **Swagger UI** (biasanya di `https://localhost:7100/swagger`).

---

## 🔐 Skenario 1: Login Owner & Pengisian Otorisasi di Swagger

Tujuan: Login menggunakan akun Owner bawaan sistem untuk mendapatkan token akses dan mengaktifkan gembok otorisasi di Swagger.

1. Buka Swagger UI di browser Anda.
2. Cari kelompok **Auth** $\rightarrow$ **`POST /api/Auth/login`**.
3. Klik tombol **Try it out**.
4. Ubah isi request body menjadi:
   ```json
   {
     "email": "owner@morruspos.com",
     "password": "owner123"
   }
   ```
5. Klik **Execute**.
6. **Hasil yang diharapkan**:
   * Kode respon: **`200 OK`**.
   * Body respon berisi `accessToken` (token string panjang) dan `refreshToken`.
7. **Salin (copy)** nilai string `accessToken` tersebut (tanpa tanda kutip).
8. Scroll ke bagian paling atas Swagger UI, lalu klik tombol **Authorize** (ikon gembok).
9. Tempelkan token yang disalin tadi ke kotak input **Value**, lalu klik **Authorize** dan klik **Close**.

---

## 🏢 Skenario 2: Mengambil Master Data Cabang & Peran (Outlets & Roles)

Tujuan: Memperoleh `OutletId` dan `RoleId` aktif dari database untuk diinputkan saat pembuatan pengguna baru.

### A. Mengambil Daftar Outlet
1. Buka kelompok **Outlets** $\rightarrow$ **`GET /api/Outlets`**.
2. Klik **Try it out**, lalu klik **Execute**.
3. **Hasil yang diharapkan**:
   * Kode respon: **`200 OK`**.
   * Salin salah satu nilai `id` outlet (misal Outlet Utama: `8bba5427-017e-40fb-886f-5e4c6c9a3809`).

### B. Mengambil Daftar Role
1. Buka kelompok **Roles** $\rightarrow$ **`GET /api/Roles`**.
2. Klik **Try it out**, lalu klik **Execute**.
3. **Hasil yang diharapkan**:
   * Kode respon: **`200 OK`**.
   * Salin salah satu nilai `id` peran, misalnya:
     * **Admin**: `d54f590a-6e54-4f05-8461-8ff62dfd8d4c`
     * **Kasir**: `b5e7d5a9-4674-4b5b-a81d-e59fa24285b0`

---

## 👤 Skenario 3: Membuat Pengguna Baru (Sebagai Owner)

Tujuan: Menguji fungsi pembuatan user baru dengan wewenang `Owner`.

1. Buka kelompok **Users** $\rightarrow$ **`POST /api/Users`**.
2. Klik **Try it out**.
3. Masukkan payload berikut (ganti `outletId` dan `roleId` dengan ID yang disalin pada Skenario 2):
   ```json
   {
     "outletId": "8bba5427-017e-40fb-886f-5e4c6c9a3809",
     "roleId": "b5e7d5a9-4674-4b5b-a81d-e59fa24285b0", 
     "name": "Budi Kasir",
     "email": "budi.kasir@morruspos.com",
     "password": "kasirpassword123"
   }
   ```
4. Klik **Execute**.
5. **Hasil yang diharapkan**:
   * Kode respon: **`201 Created`**.
   * Respon body menampilkan data pengguna "Budi Kasir" beserta ID barunya.

---

## 🚫 Skenario 4: Pembatasan Hak Akses (Membuktikan Keamanan Role)

Tujuan: Memastikan pengguna dengan hak akses rendah (Kasir) diblokir saat mencoba membuka API manajemen pengguna / outlet.

### A. Login Sebagai Kasir Baru
1. Logout dari Swagger terlebih dahulu (Klik tombol **Authorize** di atas, lalu klik **Logout**).
2. Lakukan login menggunakan akun Kasir yang baru kita buat di Skenario 3:
   * **`POST /api/Auth/login`** dengan:
     ```json
     {
       "email": "budi.kasir@morruspos.com",
       "password": "kasirpassword123"
     }
     ```
3. Salin `accessToken` kasir baru ini, lalu masukkan kembali ke tombol **Authorize** di atas Swagger.

### B. Menguji Penolakan Akses (Kasir menembak API Users)
1. Buka kelompok **Users** $\rightarrow$ **`GET /api/Users`**.
2. Klik **Try it out**, isi `outletId` dengan ID outlet, lalu klik **Execute**.
3. **Hasil yang diharapkan**:
   * Kode respon: **`403 Forbidden`**.
   * Hal ini membuktikan middleware keamanan kustom kita bekerja sempurna menolak kasir mengakses data pengguna lain.

---

## 🔄 Skenario 5: Mengubah Password Mandiri

Tujuan: Menguji kasir mengganti password mereka sendiri secara aman.

1. Buka kelompok **Users** $\rightarrow$ **`POST /api/Users/{id}/change-password`**.
2. Klik **Try it out**.
3. Isi parameter `id` di URL dengan ID user kasir tersebut (bisa dilihat dari respon login kasir).
4. Masukkan request body:
   ```json
   {
     "currentPassword": "kasirpassword123",
     "newPassword": "newkasirpassword789"
   }
   ```
5. Klik **Execute**.
6. **Hasil yang diharapkan**:
   * Kode respon: **`204 No Content`** (sukses tanpa error).
   * Coba lakukan login ulang dengan password lama (seharusnya gagal) dan login dengan password baru (seharusnya sukses).
